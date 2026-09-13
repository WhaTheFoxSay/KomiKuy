using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Mangaplus.Models;

namespace Mangaplus.Services
{
    public static class FastHtmlParser
    {
        public static List<MangaTitle> ParseCards(string html)
        {
            var list = new List<MangaTitle>(200);
            if (string.IsNullOrEmpty(html)) return list;

            var seen = new HashSet<string>();
            int pos = 0;

            while ((pos = html.IndexOf("<article class=\"ls", pos, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                int artEnd = html.IndexOf("</article>", pos, StringComparison.OrdinalIgnoreCase);
                if (artEnd == -1) artEnd = html.Length;

                string block = html.Substring(pos, artEnd - pos);

                string href = ExtractBetween(block, "href=\"/manga/", "\"");
                if (!string.IsNullOrEmpty(href))
                {
                    string fullSlug = "/manga/" + href;
                    if (!seen.Contains(fullSlug))
                    {
                        seen.Add(fullSlug);
                        
                        string title = ExtractBetween(block, "<h4>", "</h4>");
                        if (string.IsNullOrEmpty(title)) title = ExtractBetween(block, "<h3>", "</h3>");
                        title = StripTags(title);

                        string img = ExtractBetween(block, "data-src=\"", "\"");
                        if (string.IsNullOrEmpty(img)) img = ExtractBetween(block, "src=\"", "\"");
                        if (img.StartsWith("//")) img = "https:" + img;
                        if (img.Contains("thumbnail.komiku.to"))
                            img = img.Replace("thumbnail.komiku.to", "thumbnail.komiku.org");

                        string meta = ExtractBetween(block, "class=\"ls4s\">", "</span>");
                        if (string.IsNullOrEmpty(meta)) meta = ExtractBetween(block, "class=\"ls2t\">", "</span>");

                        string updatedTime = "";
                        if (!string.IsNullOrEmpty(meta))
                        {
                            string cleanMeta = StripTags(meta).Replace("Â·", "·").Trim();
                            int dot = cleanMeta.IndexOf('·');
                            if (dot != -1 && dot + 1 < cleanMeta.Length)
                                updatedTime = cleanMeta.Substring(dot + 1).Trim();
                            else
                                updatedTime = cleanMeta;
                        }
                        
                        string latestCh = "";
                        int ls24Idx = block.IndexOf("class=\"ls24\"", StringComparison.OrdinalIgnoreCase);
                        if (ls24Idx == -1) ls24Idx = block.IndexOf("class=\"ls2l\"", StringComparison.OrdinalIgnoreCase);
                        if (ls24Idx != -1)
                        {
                            int tagClose = block.IndexOf('>', ls24Idx);
                            int aClose = block.IndexOf("</a>", ls24Idx);
                            if (tagClose != -1 && aClose != -1 && aClose > tagClose)
                            {
                                latestCh = StripTags(block.Substring(tagClose + 1, aClose - tagClose - 1));
                            }
                        }

                        string comicType = ClassifyComicType(block, title, fullSlug);

                        list.Add(new MangaTitle
                        {
                            TitleId = Math.Abs(fullSlug.GetHashCode()),
                            MangaUrl = fullSlug,
                            Name = title,
                            PortraitImageUrl = img,
                            ComicType = comicType,
                            LatestChapterName = latestCh,
                            UpdatedTimeAgo = updatedTime,
                            Ranking = list.Count + 1
                        });
                    }
                }

                pos = artEnd + 10;
            }

            // Fallback for bge cards (Search results and catalog fallback)
            if (list.Count == 0)
            {
                pos = 0;
                while ((pos = html.IndexOf("<div class=\"bge\">", pos, StringComparison.OrdinalIgnoreCase)) != -1)
                {
                    int nextBge = html.IndexOf("<div class=\"bge\">", pos + 17, StringComparison.OrdinalIgnoreCase);
                    int bgeEnd = nextBge != -1 ? nextBge : html.Length;
                    string block = html.Substring(pos, bgeEnd - pos);
                    pos = bgeEnd;

                    string href = ExtractBetween(block, "href=\"/manga/", "\"");
                    if (string.IsNullOrEmpty(href)) href = ExtractBetween(block, "href=\"https://komiku.org/manga/", "\"");
                    if (string.IsNullOrEmpty(href)) href = ExtractBetween(block, "href=\"https://api.komiku.org/manga/", "\"");

                    if (!string.IsNullOrEmpty(href))
                    {
                        string fullSlug = "/manga/" + href.TrimEnd('/');
                        if (!seen.Contains(fullSlug))
                        {
                            seen.Add(fullSlug);

                            // 1. Title Extraction
                            string title = "";
                            int h3Idx = block.IndexOf("<h3", StringComparison.OrdinalIgnoreCase);
                            if (h3Idx != -1)
                            {
                                int h3Close = block.IndexOf('>', h3Idx);
                                int h3End = block.IndexOf("</h3>", h3Idx, StringComparison.OrdinalIgnoreCase);
                                if (h3Close != -1 && h3End != -1 && h3End > h3Close)
                                {
                                    title = StripTags(block.Substring(h3Close + 1, h3End - h3Close - 1));
                                }
                            }
                            if (string.IsNullOrEmpty(title)) title = ExtractBetween(block, "alt=\"", "\"");
                            if (string.IsNullOrEmpty(title)) title = ExtractBetween(block, "title=\"", "\"");
                            if (string.IsNullOrEmpty(title))
                            {
                                title = href.Replace("-", " ").Trim('/');
                            }
                            title = StripTags(System.Net.WebUtility.HtmlDecode(title)).Trim();

                            // 2. Cover Image
                            string img = ExtractBetween(block, "data-src=\"", "\"");
                            if (string.IsNullOrEmpty(img)) img = ExtractBetween(block, "src=\"", "\"");
                            if (img.StartsWith("//")) img = "https:" + img;
                            if (img.Contains("thumbnail.komiku.to"))
                                img = img.Replace("thumbnail.komiku.to", "thumbnail.komiku.org");
                            img = System.Net.WebUtility.HtmlDecode(img);

                            // 3. Update Time
                            string updateTime = "";
                            int pIdx = block.IndexOf("<p>", StringComparison.OrdinalIgnoreCase);
                            if (pIdx != -1)
                            {
                                int pEnd = block.IndexOf("</p>", pIdx, StringComparison.OrdinalIgnoreCase);
                                if (pEnd != -1)
                                {
                                    string pText = StripTags(block.Substring(pIdx + 3, pEnd - pIdx - 3)).Trim();
                                    if (pText.StartsWith("Update ", StringComparison.OrdinalIgnoreCase))
                                        updateTime = pText.Substring(7).TrimEnd('.');
                                    else
                                        updateTime = pText;
                                }
                            }

                            // 4. Latest Chapter
                            string latestCh = "";
                            int terbIdx = block.IndexOf("Terbaru:", StringComparison.OrdinalIgnoreCase);
                            if (terbIdx != -1)
                            {
                                int spanOpen = block.IndexOf("<span>", terbIdx, StringComparison.OrdinalIgnoreCase);
                                if (spanOpen != -1)
                                {
                                    int spanClose = block.IndexOf("</span>", spanOpen, StringComparison.OrdinalIgnoreCase);
                                    if (spanClose != -1)
                                    {
                                        latestCh = StripTags(block.Substring(spanOpen + 6, spanClose - spanOpen - 6)).Trim();
                                    }
                                }
                                if (string.IsNullOrEmpty(latestCh))
                                {
                                    int aEnd = block.IndexOf("</a>", terbIdx, StringComparison.OrdinalIgnoreCase);
                                    if (aEnd != -1)
                                    {
                                        latestCh = StripTags(block.Substring(terbIdx, aEnd - terbIdx)).Trim();
                                    }
                                }
                            }

                            // 5. Comic Type & Genre
                            string comicType = ClassifyComicType(block, title, fullSlug);
                            string genre = "";
                            int tpeIdx = block.IndexOf("class=\"tpe1_inf\"", StringComparison.OrdinalIgnoreCase);
                            if (tpeIdx != -1)
                            {
                                int tpeClose = block.IndexOf("</div>", tpeIdx, StringComparison.OrdinalIgnoreCase);
                                if (tpeClose != -1)
                                {
                                    string tpeText = StripTags(block.Substring(tpeIdx, tpeClose - tpeIdx)).Trim();
                                    genre = tpeText.Replace("Manhwa", "").Replace("Manhua", "").Replace("Manga", "").Trim();
                                }
                            }

                            list.Add(new MangaTitle
                            {
                                TitleId = Math.Abs(fullSlug.GetHashCode()),
                                MangaUrl = fullSlug,
                                Name = title,
                                PortraitImageUrl = img,
                                ComicType = comicType,
                                Theme = genre,
                                LatestChapterName = latestCh,
                                UpdatedTimeAgo = updateTime,
                                Ranking = list.Count + 1
                            });
                        }
                    }
                }
            }

            return list;
        }

        public static string ClassifyComicType(string block, string title, string slug)
        {
            if (string.IsNullOrEmpty(block)) block = "";
            if (string.IsNullOrEmpty(title)) title = "";
            if (string.IsNullOrEmpty(slug)) slug = "";

            // 1. Direct explicit tags, flags, and attributes in HTML
            if (block.IndexOf("kr.png", StringComparison.OrdinalIgnoreCase) != -1 ||
                block.IndexOf("alt=\"Baca Manhwa", StringComparison.OrdinalIgnoreCase) != -1 ||
                block.IndexOf("title=\"Baca Manhwa", StringComparison.OrdinalIgnoreCase) != -1 ||
                block.IndexOf("data-tipe=\"Manhwa\"", StringComparison.OrdinalIgnoreCase) != -1 ||
                block.IndexOf(">Manhwa<", StringComparison.OrdinalIgnoreCase) != -1 ||
                slug.IndexOf("manhwa", StringComparison.OrdinalIgnoreCase) != -1)
            {
                return "Manhwa";
            }

            if (block.IndexOf("cn.png", StringComparison.OrdinalIgnoreCase) != -1 ||
                block.IndexOf("alt=\"Baca Manhua", StringComparison.OrdinalIgnoreCase) != -1 ||
                block.IndexOf("title=\"Baca Manhua", StringComparison.OrdinalIgnoreCase) != -1 ||
                block.IndexOf("data-tipe=\"Manhua\"", StringComparison.OrdinalIgnoreCase) != -1 ||
                block.IndexOf(">Manhua<", StringComparison.OrdinalIgnoreCase) != -1 ||
                slug.IndexOf("manhua", StringComparison.OrdinalIgnoreCase) != -1)
            {
                return "Manhua";
            }

            if (block.IndexOf("jp.png", StringComparison.OrdinalIgnoreCase) != -1 ||
                block.IndexOf("alt=\"Baca Manga", StringComparison.OrdinalIgnoreCase) != -1 ||
                block.IndexOf("title=\"Baca Manga", StringComparison.OrdinalIgnoreCase) != -1 ||
                block.IndexOf("data-tipe=\"Manga\"", StringComparison.OrdinalIgnoreCase) != -1)
            {
                return "Manga";
            }

            // Special exception: Wind Breaker Japanese delinquent manga by NII Satoru
            if (slug.IndexOf("wind-breaker-nii-satoru", StringComparison.OrdinalIgnoreCase) != -1 ||
                title.IndexOf("NII Satoru", StringComparison.OrdinalIgnoreCase) != -1)
            {
                return "Manga";
            }

            string combined = (title + " " + slug).ToLowerInvariant();

            // 2. Comprehensive Korean Webtoon / Manhwa indicators
            if (combined.Contains("tomb-raider") ||
                combined.Contains("strongest-under-heaven") ||
                combined.Contains("under-heaven") ||
                combined.Contains("my-dad-is") ||
                combined.Contains("leveling") ||
                combined.Contains("hunter") ||
                combined.Contains("ranker") ||
                combined.Contains("max-level") ||
                combined.Contains("standard-of-reincarnation") ||
                combined.Contains("swordmaster") ||
                combined.Contains("nano-machine") ||
                combined.Contains("regressed-mercenary") ||
                combined.Contains("mercenary") ||
                combined.Contains("solo-leveling") ||
                combined.Contains("beginning-after-the-end") ||
                combined.Contains("infinite-mage") ||
                combined.Contains("lookism") ||
                combined.Contains("omniscient") ||
                combined.Contains("eleceed") ||
                combined.Contains("greatest-estate") ||
                combined.Contains("mount-hua") ||
                combined.Contains("northern-blade") ||
                combined.Contains("second-life") ||
                combined.Contains("hero-has-returned") ||
                combined.Contains("murim") ||
                combined.Contains("deadbeat-noble") ||
                combined.Contains("past-life") ||
                combined.Contains("sss-class") ||
                combined.Contains("chaebol") ||
                combined.Contains("auto-hunting") ||
                combined.Contains("boundless-necromancer") ||
                combined.Contains("reaper-of-the-drifting") ||
                combined.Contains("worthless-regression") ||
                combined.Contains("suicidal-battle-god") ||
                combined.Contains("villainess") ||
                combined.Contains("duchess") ||
                combined.Contains("tyrant") ||
                combined.Contains("dungeon") ||
                combined.Contains("constellation") ||
                combined.Contains("regressor") ||
                combined.Contains("regression") ||
                combined.Contains("talent-swallowing") ||
                combined.Contains("skeleton-soldier") ||
                combined.Contains("pick-me-up"))
            {
                return "Manhwa";
            }

            // 3. Comprehensive Chinese Manhua indicators
            if (combined.Contains("martial-peak") ||
                combined.Contains("magic-emperor") ||
                combined.Contains("apotheosis") ||
                combined.Contains("tales-of-demons") ||
                combined.Contains("tales-demons") ||
                combined.Contains("yuan-zun") ||
                combined.Contains("battle-through-the-heavens") ||
                combined.Contains("cultivat") ||
                combined.Contains("sovereign") ||
                combined.Contains("daoist") ||
                combined.Contains("martial-god") ||
                combined.Contains("son-in-law") ||
                combined.Contains("tycoons") ||
                combined.Contains("licking-gold") ||
                combined.Contains("full-time-awakening") ||
                combined.Contains("disastrous-necromancer") ||
                combined.Contains("god-level-assassin") ||
                combined.Contains("alter-egos") ||
                combined.Contains("devious-son") ||
                combined.Contains("urban-immortal") ||
                combined.Contains("ultimate-of-all-ages") ||
                combined.Contains("star-martial-god") ||
                combined.Contains("god-of-martial"))
            {
                return "Manhua";
            }

            return "Manga";
        }

        public static void ParseMangaDetail(string html, MangaTitle title, List<ChapterItem> chapters, string baseUrl)
        {
            if (string.IsNullOrEmpty(html) || title == null) return;

            // 1. Title
            int h1Start = html.IndexOf("<h1", StringComparison.OrdinalIgnoreCase);
            if (h1Start != -1)
            {
                int h1End = html.IndexOf("</h1>", h1Start, StringComparison.OrdinalIgnoreCase);
                if (h1End != -1)
                {
                    string h1Cleaned = StripTags(html.Substring(h1Start, h1End - h1Start));
                    if (h1Cleaned.StartsWith("Komik ", StringComparison.OrdinalIgnoreCase))
                        h1Cleaned = h1Cleaned.Substring(6).Trim();
                    if (!string.IsNullOrEmpty(h1Cleaned))
                        title.Name = h1Cleaned;
                }
            }

            // 2. Cover Image
            string imsBlock = ExtractBetween(html, "<div class=\"ims\">", "</div>");
            string coverUrl = ExtractBetween(imsBlock, "data-src=\"", "\"");
            if (string.IsNullOrEmpty(coverUrl)) coverUrl = ExtractBetween(imsBlock, "src=\"", "\"");
            if (string.IsNullOrEmpty(coverUrl)) coverUrl = ExtractBetween(html, "itemprop=\"image\" src=\"", "\"");
            if (string.IsNullOrEmpty(coverUrl)) coverUrl = ExtractBetween(html, "class=\"lazy\" data-src=\"", "\"");
            if (string.IsNullOrEmpty(coverUrl)) coverUrl = ExtractBetween(html, "<meta property=\"og:image\" content=\"", "\"");
            if (!string.IsNullOrEmpty(coverUrl))
            {
                if (coverUrl.StartsWith("//")) coverUrl = "https:" + coverUrl;
                if (coverUrl.Contains("thumbnail.komiku.to"))
                    coverUrl = coverUrl.Replace("thumbnail.komiku.to", "thumbnail.komiku.org");
                coverUrl = System.Net.WebUtility.HtmlDecode(coverUrl);
                title.PortraitImageUrl = coverUrl;
            }

            // 3. Metadata Table
            int infStart = html.IndexOf("<table class=\"inftable\"", StringComparison.OrdinalIgnoreCase);
            if (infStart != -1)
            {
                int infEnd = html.IndexOf("</table>", infStart, StringComparison.OrdinalIgnoreCase);
                if (infEnd != -1)
                {
                    string infBlock = html.Substring(infStart, infEnd - infStart);
                    int trPos = 0;
                    while ((trPos = infBlock.IndexOf("<tr>", trPos, StringComparison.OrdinalIgnoreCase)) != -1)
                    {
                        int trEnd = infBlock.IndexOf("</tr>", trPos, StringComparison.OrdinalIgnoreCase);
                        if (trEnd == -1) break;

                        string tr = infBlock.Substring(trPos, trEnd - trPos);
                        string k = StripTags(ExtractBetween(tr, "<td", "</td>")).TrimEnd(':').Trim().ToLowerInvariant();
                        
                        string v = "";
                        int secondTd = tr.IndexOf("</td>", StringComparison.OrdinalIgnoreCase);
                        if (secondTd != -1)
                        {
                            v = StripTags(ExtractBetween(tr.Substring(secondTd + 5), "<td", "</td>"));
                        }

                        if (k.Contains("judul alternatif")) title.AlternativeName = v;
                        else if (k.Contains("pengarang") || k.Contains("author") || k.Contains("penulis")) title.Author = v;
                        else if (k.Contains("tipe") || k.Contains("jenis")) title.ComicType = v;
                        else if (k.Contains("tema")) title.Theme = v;
                        else if (k.Contains("status")) title.Status = v;
                        else if (k.Contains("rating")) title.Rating = v;
                        else if (k.Contains("pembaca") || k.Contains("views")) title.ViewCount = v;
                        else if (k.Contains("cara baca")) title.ReadingDirection = v;
                        else if (k.Contains("genre"))
                        {
                            var genres = new List<string>();
                            int gIdx = 0;
                            while ((gIdx = tr.IndexOf("<a", gIdx, StringComparison.OrdinalIgnoreCase)) != -1)
                            {
                                string g = StripTags(ExtractBetween(tr.Substring(gIdx), ">", "</a>"));
                                if (!string.IsNullOrEmpty(g)) genres.Add(g);
                                gIdx += 5;
                            }
                            title.Genres = genres;
                        }

                        trPos = trEnd + 5;
                    }
                }
            }

            // 4. Clean Synopsis (Free of any raw HTML tags or styles)
            int sinIdx = html.IndexOf("id=\"Sinopsis\"", StringComparison.OrdinalIgnoreCase);
            if (sinIdx == -1) sinIdx = html.IndexOf("class=\"desc\"", StringComparison.OrdinalIgnoreCase);
            if (sinIdx != -1)
            {
                int tagClose = html.IndexOf('>', sinIdx);
                int secEnd = html.IndexOf("</section>", sinIdx);
                if (secEnd == -1) secEnd = html.IndexOf("</p>", sinIdx);
                if (secEnd == -1) secEnd = sinIdx + 800;

                if (tagClose != -1 && secEnd > tagClose)
                {
                    string rawSin = html.Substring(tagClose + 1, secEnd - tagClose - 1);
                    string cleanSin = StripTags(rawSin);

                    // Remove redundant header prefix
                    if (cleanSin.StartsWith("Sinopsis Lengkap Komik", StringComparison.OrdinalIgnoreCase))
                    {
                        int pEnd = cleanSin.IndexOf('!');
                        if (pEnd != -1 && pEnd + 1 < cleanSin.Length)
                            cleanSin = cleanSin.Substring(pEnd + 1).Trim();
                    }
                    else if (cleanSin.StartsWith("Sinopsis", StringComparison.OrdinalIgnoreCase))
                    {
                        cleanSin = cleanSin.Substring(8).Trim();
                    }

                    if (!string.IsNullOrEmpty(cleanSin))
                        title.Synopsis = cleanSin;
                }
            }

            // 5. Chapters
            var seenHrefs = new HashSet<string>();
            int pos = 0;
            string trMarker = "<tr itemprop=\"itemListElement\"";
            while ((pos = html.IndexOf(trMarker, pos, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                int trEnd = html.IndexOf("</tr>", pos, StringComparison.OrdinalIgnoreCase);
                if (trEnd == -1) trEnd = html.Length;

                string trBlock = html.Substring(pos, trEnd - pos);
                string href = ExtractBetween(trBlock, "href=\"", "\"");
                string name = ExtractBetween(trBlock, "<span><b>", "</b>");
                if (string.IsNullOrEmpty(name)) name = ExtractBetween(trBlock, "<b>", "</b>");
                if (string.IsNullOrEmpty(name)) name = ExtractBetween(trBlock, "<span>", "</span>");
                name = StripTags(name);

                string date = StripTags(ExtractBetween(trBlock, "class=\"tanggalseries\">", "</td>"));

                if (!string.IsNullOrEmpty(href))
                {
                    if (!href.StartsWith("http")) href = $"{baseUrl}{href}";

                    if (!seenHrefs.Contains(href))
                    {
                        seenHrefs.Add(href);
                        double num = 0;
                        int digitStart = -1;
                        for (int k = 0; k < name.Length; k++)
                        {
                            if (char.IsDigit(name[k])) { digitStart = k; break; }
                        }
                        if (digitStart != -1)
                        {
                            int digitEnd = digitStart;
                            while (digitEnd < name.Length && (char.IsDigit(name[digitEnd]) || name[digitEnd] == '.'))
                                digitEnd++;
                            double.TryParse(name.Substring(digitStart, digitEnd - digitStart), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out num);
                        }

                        chapters.Add(new ChapterItem
                        {
                            ChapterId = chapters.Count + 1,
                            TitleId = title.TitleId,
                            TitleName = title.Name,
                            ThumbnailUrl = title.PortraitImageUrl,
                            Name = string.IsNullOrEmpty(name) ? "Chapter " + (chapters.Count + 1) : name,
                            ChapterUrl = href,
                            ReleaseDateString = date,
                            ChapterNumber = num > 0 ? num : (chapters.Count + 1),
                            IsFree = true,
                            IsLocked = false
                        });
                    }
                }

                pos = trEnd + 5;
            }

            // Fallback for general chapter links
            if (chapters.Count == 0)
            {
                int aPos = 0;
                while ((aPos = html.IndexOf("<a", aPos, StringComparison.OrdinalIgnoreCase)) != -1)
                {
                    int aEnd = html.IndexOf("</a>", aPos, StringComparison.OrdinalIgnoreCase);
                    if (aEnd == -1) break;

                    string aBlock = html.Substring(aPos, aEnd - aPos + 4);
                    string href = ExtractBetween(aBlock, "href=\"", "\"");
                    if (!string.IsNullOrEmpty(href) && (href.Contains("chapter") || href.Contains("/ch/")))
                    {
                        if (!href.StartsWith("http")) href = $"{baseUrl}{href}";
                        if (!seenHrefs.Contains(href))
                        {
                            seenHrefs.Add(href);
                            string text = StripTags(aBlock);
                            if (string.IsNullOrEmpty(text)) text = "Chapter " + (chapters.Count + 1);
                            double num = ChapterItem.ExtractChapterNumber(text, "", chapters.Count + 1);

                            chapters.Add(new ChapterItem
                            {
                                ChapterId = chapters.Count + 1,
                                TitleId = title.TitleId,
                                TitleName = title.Name,
                                ThumbnailUrl = title.PortraitImageUrl,
                                Name = text,
                                ChapterUrl = href,
                                ChapterNumber = num,
                                IsFree = true,
                                IsLocked = false
                            });
                        }
                    }

                    aPos = aEnd + 4;
                }
            }

            chapters.Sort((a, b) => a.ChapterNumber.CompareTo(b.ChapterNumber));
            for (int i = 0; i < chapters.Count; i++)
            {
                chapters[i].ChapterId = i + 1;
            }

            if (chapters.Count > 0)
            {
                title.FirstChapterName = chapters[0].DisplayName;
                title.FirstChapterUrl = chapters[0].ChapterUrl;
                title.LatestChapterName = chapters[chapters.Count - 1].DisplayName;
                title.LatestChapterUrl = chapters[chapters.Count - 1].ChapterUrl;
            }
        }

        public static List<MangaPageItem> ParseChapterPages(string html)
        {
            var pages = new List<MangaPageItem>();
            if (string.IsNullOrEmpty(html)) return pages;

            int bkStart = html.IndexOf("id=\"Baca_Komik\"", StringComparison.OrdinalIgnoreCase);
            string scope = html;
            if (bkStart != -1)
            {
                int bkEnd = html.IndexOf("</div>\n<div", bkStart, StringComparison.OrdinalIgnoreCase);
                if (bkEnd == -1) bkEnd = html.IndexOf("</div>\r\n<div", bkStart, StringComparison.OrdinalIgnoreCase);
                if (bkEnd != -1) scope = html.Substring(bkStart, bkEnd - bkStart);
            }

            var pageSet = new HashSet<string>();
            int pos = 0;
            while ((pos = scope.IndexOf("<img", pos, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                int imgEnd = scope.IndexOf('>', pos);
                if (imgEnd == -1) break;

                string imgTag = scope.Substring(pos, imgEnd - pos);
                string url = ExtractBetween(imgTag, "src=\"", "\"");
                if (string.IsNullOrEmpty(url) || url.Contains("data:image"))
                {
                    url = ExtractBetween(imgTag, "data-src=\"", "\"");
                }

                if (!string.IsNullOrEmpty(url) &&
                    !url.Contains("wmkomiku") &&
                    !url.Contains("iklan") &&
                    !url.Contains("logo") &&
                    !url.Contains(".ico") &&
                    !url.Contains("Loading.gif") &&
                    !url.Contains("komikuplus"))
                {
                    if (url.StartsWith("//")) url = "https:" + url;

                    if (url.Contains("komiku.to"))
                    {
                        url = Regex.Replace(url, @"https?://image\d*\.komiku\.to", "https://img.komiku.org");
                        url = url.Replace("image.komiku.to", "img.komiku.org");
                        url = url.Replace("thumbnail.komiku.to", "thumbnail.komiku.org");
                    }

                    if (!pageSet.Contains(url) && (url.Contains(".png") || url.Contains(".jpg") || url.Contains(".webp") || url.Contains(".jpeg")))
                    {
                        pageSet.Add(url);
                        pages.Add(new MangaPageItem
                        {
                            PageNumber = pages.Count + 1,
                            ImageUrl = url,
                            EncryptionKeyHex = "",
                            IsSpread = false,
                            IsLastPage = false
                        });
                    }
                }

                pos = imgEnd + 1;
            }

            if (pages.Count > 0)
            {
                pages[pages.Count - 1].IsLastPage = true;
            }

            return pages;
        }

        public static string ExtractBetween(string src, string start, string end)
        {
            if (string.IsNullOrEmpty(src)) return "";
            int s = src.IndexOf(start, StringComparison.OrdinalIgnoreCase);
            if (s == -1) return "";
            s += start.Length;
            int e = src.IndexOf(end, s, StringComparison.OrdinalIgnoreCase);
            if (e == -1) return "";
            return src.Substring(s, e - s).Trim();
        }

        public static string StripTags(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            bool inTag = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '<') inTag = true;
                else if (c == '>') inTag = false;
                else if (!inTag) sb.Append(c);
            }
            string result = WebUtility.HtmlDecode(sb.ToString()).Trim();
            return Regex.Replace(result, @"\s+", " ");
        }
    }
}
