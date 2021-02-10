using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace DHIS2Xfer.Factory
{
    public class HTTPFactory
    {
        public static string HTTPGet(string url, string username, string password)
        {
            string token = username + ":" + password;
            token = Convert.ToBase64String(Encoding.Default.GetBytes(token));

            HttpClient client = new HttpClient();

            try
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);

                var response = client.GetStringAsync(url);
                string result = response.Result.ToString();

                return result;
            }
            catch (Exception ex)
            {


                return "";

            }
        }
    }
}
