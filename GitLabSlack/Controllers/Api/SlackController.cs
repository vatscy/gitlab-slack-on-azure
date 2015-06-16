﻿using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace GitLabSlack.Controllers.Api
{
    /// <summary>
    /// Slack APIに中継するAPIを提供します。
    /// </summary>
    public class SlackController : ApiController
    {
        /// <summary>
        /// GitLab ドメイン
        /// </summary>
        private static readonly string _gitlabDomain = "https://xxx.com/"; // TODO

        /// <summary>
        /// Slack API トークン
        /// </summary>
        private static readonly string _token = "xxxx-xxxxxxxxxx-xxxxxxxxxx-xxxxxxxxxx-xxxxxx"; // TODO

        /// <summary>
        /// GitLab Hookの受け口です。
        /// </summary>
        /// <param name="channel">チャンネル名</param>
        /// <param name="body">リクエストボディ</param>
        /// <returns></returns>
        [HttpPost]
        public async Task Post(string channel, [FromBody] JObject body)
        {
            await Proxy(body, channel, x => !string.IsNullOrEmpty(channel));
        }

        /// <summary>
        /// GitLab Hook を受信し、Slack の PostMessage API をコールします。
        /// </summary>
        /// <param name="body">リクエストボディ</param>
        /// <param name="channel">チャンネル名</param>
        /// <param name="isValid">Slack へ POST する、しないの条件</param>
        /// <returns></returns>
        private async Task Proxy(JObject body, string channel, Func<JObject, bool> isValid)
        {
            try
            {
                if (isValid(body))
                {
                    var objectAttr = body["object_attributes"];

                    var updateTime = DateTime.ParseExact(objectAttr["updated_at"].Value<string>(), "yyyy-MM-dd HH:mm:ss' UTC'", null).AddHours(9).ToString("yyyy/MM/dd'T'HH:mm:ss");
                    var title = objectAttr["title"].Value<string>();
                    var description = objectAttr["description"].Value<string>();
                    var nameSpace = objectAttr["source"]["namespace"].Value<string>().ToLower().Replace(' ', '-');
                    var name = objectAttr["source"]["name"].Value<string>().ToLower();
                    var iid = objectAttr["iid"].Value<string>();
                    var mergeRequestUrl = _gitlabDomain + nameSpace + "/" + name + "/merge_requests/" + iid;

                    var message = new StringBuilder()
                        .Append("Merge Request was created at ").AppendLine(updateTime)
                        .AppendLine(title)
                        .AppendLine(description)
                        .AppendLine(mergeRequestUrl)
                        .ToString();

                    var url = "https://slack.com/api/chat.postMessage?link_names=1&username=GitLab"
                        + "&token=" + _token
                        + "&channel=%23" + channel
                        + "&text=" + HttpUtility.UrlEncode(message, Encoding.UTF8)
                        + "&icon_url=" + HttpUtility.UrlEncode("http://gitlabslack.azurewebsites.net/content/img/gitlab.jpg", Encoding.UTF8);

                    await PostToSlackAsync(url);
                }
            }
            catch
            {
                return;
            }
        }

        private async Task<WebResponse> PostToSlackAsync(string url)
        {
            ServicePointManager.ServerCertificateValidationCallback = (sndr, certificate, chain, sslPolicyErrors) => true;

            var request = HttpWebRequest.CreateHttp(url) as HttpWebRequest;
            request.Method = HttpMethod.Get.Method;

            return await request.GetResponseAsync();
        }
    }
}
