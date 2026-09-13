using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace Mangaplus.Services
{
    public class MangaImageDecryptor
    {
        private static MangaImageDecryptor _instance;
        public static MangaImageDecryptor Instance => _instance ?? (_instance = new MangaImageDecryptor());

        private readonly HttpClient _httpClient;
        private StorageFolder _cacheFolder;
        private readonly SemaphoreSlim _downloadSemaphore = new SemaphoreSlim(6, 6);

        public MangaImageDecryptor()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                MaxConnectionsPerServer = 16
            };
            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://komiku.org/");
        }

        private async Task EnsureCacheFolderAsync()
        {
            if (_cacheFolder == null)
            {
                try
                {
                    _cacheFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("MangaCache", CreationCollisionOption.OpenIfExists);
                }
                catch { }
            }
        }

        public byte[] Decrypt(byte[] encryptedBytes, string keyHex)
        {
            if (encryptedBytes == null || encryptedBytes.Length == 0)
                return encryptedBytes;

            if (string.IsNullOrWhiteSpace(keyHex))
                return encryptedBytes;

            try
            {
                int keyLen = keyHex.Length / 2;
                byte[] keyBytes = new byte[keyLen];
                for (int i = 0; i < keyLen; i++)
                {
                    keyBytes[i] = Convert.ToByte(keyHex.Substring(i * 2, 2), 16);
                }

                byte[] decrypted = new byte[encryptedBytes.Length];
                for (int i = 0; i < encryptedBytes.Length; i++)
                {
                    decrypted[i] = (byte)(encryptedBytes[i] ^ keyBytes[i % keyLen]);
                }

                return decrypted;
            }
            catch
            {
                return encryptedBytes;
            }
        }

        public async Task<BitmapImage> LoadImageAsync(string imageUrl, string encryptionKeyHex = "", int targetWidth = 540)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return null;

            try
            {
                byte[] decryptedBytes = await GetImageBytesAsync(imageUrl, encryptionKeyHex);
                if (decryptedBytes == null || decryptedBytes.Length == 0)
                    return null;

                var stream = new InMemoryRandomAccessStream();
                await stream.WriteAsync(decryptedBytes.AsBuffer());
                stream.Seek(0);

                var bmp = new BitmapImage();
                // 540px provides crisp 1.15x HD on 480px screen and takes only 0.8 MB per image in RAM
                bmp.DecodePixelWidth = targetWidth > 0 ? targetWidth : 540;
                await bmp.SetSourceAsync(stream);
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        public async Task<byte[]> GetImageBytesAsync(string imageUrl, string encryptionKeyHex = "")
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return null;

            // Normalize server host
            if (imageUrl.Contains("komiku.to"))
            {
                imageUrl = System.Text.RegularExpressions.Regex.Replace(imageUrl, @"https?://image\d*\.komiku\.to", "https://img.komiku.org");
                imageUrl = imageUrl.Replace("image.komiku.to", "img.komiku.org");
                imageUrl = imageUrl.Replace("thumbnail.komiku.to", "thumbnail.komiku.org");
            }

            await EnsureCacheFolderAsync();
            string cacheKey = GetCacheKey(imageUrl);

            // 1. Check local disk cache (Instant 0 ms)
            if (_cacheFolder != null)
            {
                try
                {
                    var file = await _cacheFolder.TryGetItemAsync(cacheKey) as StorageFile;
                    if (file != null)
                    {
                        var buffer = await FileIO.ReadBufferAsync(file);
                        return buffer.ToArray();
                    }
                }
                catch { }
            }

            // 2. Download from CDN
            await _downloadSemaphore.WaitAsync();
            try
            {
                if (_cacheFolder != null)
                {
                    try
                    {
                        var file = await _cacheFolder.TryGetItemAsync(cacheKey) as StorageFile;
                        if (file != null)
                        {
                            var buffer = await FileIO.ReadBufferAsync(file);
                            return buffer.ToArray();
                        }
                    }
                    catch { }
                }

                byte[] raw = await _httpClient.GetByteArrayAsync(imageUrl);
                byte[] decrypted = Decrypt(raw, encryptionKeyHex);

                // 3. Save to local disk cache
                if (_cacheFolder != null && decrypted != null && decrypted.Length > 0)
                {
                    try
                    {
                        var file = await _cacheFolder.CreateFileAsync(cacheKey, CreationCollisionOption.ReplaceExisting);
                        await FileIO.WriteBytesAsync(file, decrypted);
                    }
                    catch { }
                }

                return decrypted;
            }
            catch
            {
                // Fallback to img.komiku.org / thumbnail.komiku.org if another host failed
                if (imageUrl.Contains("komiku.to"))
                {
                    try
                    {
                        string fallbackUrl = System.Text.RegularExpressions.Regex.Replace(imageUrl, @"https?://image\d*\.komiku\.to", "https://img.komiku.org");
                        fallbackUrl = fallbackUrl.Replace("image.komiku.to", "img.komiku.org");
                        fallbackUrl = fallbackUrl.Replace("thumbnail.komiku.to", "thumbnail.komiku.org");
                        byte[] raw = await _httpClient.GetByteArrayAsync(fallbackUrl);
                        return raw;
                    }
                    catch { }
                }
                return null;
            }
            finally
            {
                _downloadSemaphore.Release();
            }
        }

        public void PreloadImagesInBackground(IEnumerable<string> imageUrls)
        {
            if (imageUrls == null) return;
            Task.Run(async () =>
            {
                foreach (var url in imageUrls)
                {
                    if (!string.IsNullOrEmpty(url))
                    {
                        await GetImageBytesAsync(url);
                    }
                }
            });
        }

        private string GetCacheKey(string url)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(url));
                var sb = new StringBuilder();
                foreach (byte b in hash)
                    sb.Append(b.ToString("x2"));
                return sb.ToString().Substring(0, 24) + ".jpg";
            }
        }
    }
}
