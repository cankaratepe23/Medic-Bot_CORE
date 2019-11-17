// YoutubeSearch
// It is a library for .NET, written in C#, to show search query results from YouTube.
//
// (c) 2016 Torsten Klinger - torsten.klinger(at)googlemail.com
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see<http://www.gnu.org/licenses/>.

using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace YoutubeSearch
{
    public class VideoSearch
    {
        List<VideoInformation> items;

        WebClient webclient;

        string title;
        string author;
        string duration;
        string url;
        string thumbnail;

        /// <summary>
        /// Doing search query with given parameters. Returns a List<> object.
        /// </summary>
        /// <param name="queryString"></param>
        /// <param name="queryPages"></param>
        /// <returns></returns>
        public List<VideoInformation> SearchQuery(string queryString, int queryPages)
        {
            items = new List<VideoInformation>();

            webclient = new WebClient();
            webclient.Encoding = System.Text.Encoding.UTF8;

            // Do search
            for (int i = 1; i <= queryPages; i++)
            {
                // Search address
                string html = webclient.DownloadString("https://www.youtube.com/results?search_query=" + queryString + "&page=" + i + "&sp=EgIQAQ%253D%253D");

                // Search string
                string pattern = "<div class=\"yt-lockup-content\">.*?title=\"(?<NAME>.*?)\".*?</div></div></div></li>"; //THIS PATTERN SPLITS SEARCH RESULT DIVS BY VIDEOS. EACH MEMBER OF result IS A SEPERATE VIDEO RESULT
                MatchCollection result = Regex.Matches(html, pattern, RegexOptions.Singleline);

                for (int ctr = 0; ctr <= result.Count - 1; ctr++)
                {
                    // Title
                    title = result[ctr].Groups[1].Value;

                    // Author
                    // yt-uix-sessionlink       spf-link \" data-sessionlink=\"itct=
                    // </a></div><div class=\"yt-lockup-meta \">
                    author = VideoItemHelper.Cull(result[ctr].Value, "yt-uix-sessionlink       spf-link \" data-sessionlink=\"itct=", "</a>"); //.Substring(44); //.Replace('"', ' ').TrimStart().TrimEnd();
                    author = author.Substring(45);

                    // Duration
                    duration = VideoItemHelper.Cull(VideoItemHelper.Cull(result[ctr].Value, "id=\"description-id-", "span"), ": ", ".<");

                    // Url
                    url = string.Concat("http://www.youtube.com/watch?v=", VideoItemHelper.Cull(result[ctr].Value, "watch?v=", "\""));

                    // Thumbnail
                    thumbnail = "https://i.ytimg.com/vi/" + VideoItemHelper.Cull(result[ctr].Value, "watch?v=", "\"") + "/mqdefault.jpg";

                    //// Remove playlists
                    //if (title != "__title__")
                    //{
                    //    if (duration != "")
                    //    {
                    //        // Add item to list
                    //        items.Add(new VideoInformation() { Title = title, Url = url, Duration = duration, Thumbnail = thumbnail, Author = author });
                    //    }
                    //}
                    items.Add(new VideoInformation() { Title = title, Url = url, Duration = duration, Thumbnail = thumbnail, Author = author });
                }
            }

            return items;
        }
    }
}