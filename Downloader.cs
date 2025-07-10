using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Crossbill.LicenseNoticeAggregator
{
    public static class Downloader
    {
        public static MemoryStream DownloadLicense(string apiUrl, bool isIgnoreErrors)
        {
            using (HttpClientHandler handler = new HttpClientHandler())
            {
                handler.AllowAutoRedirect = true;
                using (HttpClient client = GetClient(handler))
                {
                    // List data response.
                    HttpResponseMessage response = client.GetAsync(apiUrl).Result;  // Blocking call!
                    if (response.IsSuccessStatusCode)
                    {
                        MemoryStream memory = new MemoryStream();
                        using (var stream = response.Content.ReadAsStreamAsync().Result)
                        {
                            CopyStream(stream, memory);
                        }
                        memory.Position = 0;
                        return memory;
                    }
                    else if (isIgnoreErrors)
                    {
                        return null;
                    }
                    else
                    {
                        var error = GetError(apiUrl, response);
                        if (error == null)
                        {
                            return null;
                        }
                        throw error;
                    }
                }
            }
        }

        private static void CopyStream(Stream input, Stream output)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            byte[] buffer = new byte[8 * 1024];
            int len;
            while ((len = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, len);
            }
        }

        private static HttpClient GetClient(HttpClientHandler handler)
        {
            HttpClient client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromMinutes(5);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
            return client;
        }

        private static Exception FormatError(string message, string url, string response)
        {
            Exception inner = null;
            if (message != null)
            {
                inner = new Exception(message);
            }
            var ex = new Exception("Remote server call failed.", inner);
            if (!String.IsNullOrEmpty(url))
            {
                ex.Data.Add("RemoteUrl", url);
            }
            if (!String.IsNullOrEmpty(response))
            {
                ex.Data.Add("Response", response);
            }
            return ex;
        }

        private static Exception GetError(string apiUrl, HttpResponseMessage response)
        {
            Exception ex = null;
            if (response != null && response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                ex = new UnauthorizedAccessException(response.ReasonPhrase);
            }
            else if (response != null && response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            {
                ex = new Exception(response.ReasonPhrase);
            }

            if (ex != null)
            {
                if (!String.IsNullOrEmpty(apiUrl))
                {
                    ex.Data.Add("RemoteUrl", apiUrl);
                }
                throw ex;
            }

            string result = null;
            if (response != null && response.Content != null)
            {
                result = response.Content.ReadAsStringAsync().Result;
            }

            return FormatError(response.ReasonPhrase, apiUrl, result);
        }
    }
}
