using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace XferCore
{
	class HTTPFactory
    {
		private static HttpClient client = new HttpClient();

		public static async Task<string> GET(string url,string username, string password)
        {
			try
			{
				
				client.DefaultRequestHeaders.Accept.Clear();
				client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", GenerateToken(username,password));

				var response = client.GetStringAsync(url);
				string result = response.Result.ToString();

				return result;
			}
			catch (Exception ex)
			{

				return "";
			}
        }

		public static async Task<string> POST(string url, string username, string password, string data)
		{
			try
			{

				var postData = new StringContent(data, Encoding.UTF8, "application/json");
				client.DefaultRequestHeaders.Accept.Clear();
				client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", GenerateToken(username, password));

				var response = await client.PostAsync(url, postData);
				string result = response.Content.ReadAsStringAsync().Result;

				return result;
			}
			catch (Exception ex)
			{

				return "";
			}
		}

		private static string GenerateToken(string username, string password)
		{
			string token = username + ":" + password;
			token = Convert.ToBase64String(Encoding.Default.GetBytes(token));

			return token;
		}
    }
}
