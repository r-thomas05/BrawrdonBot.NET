﻿using System;
using System.IO;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace BrawrdonBot.Server
{
    internal static class Listener
    {
        private static HttpListener _listener;
        private static HttpClient _client;

        private static BrawrdonBot _brawrdonBot;

        private static void Main(string[] args)
        {
            // TODO: Add exceptions because this broke when I tried to ping
            _client = new HttpClient();
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://localhost:15101/");

            // Build bots
            Build();

            // Start listener
            _listener.Start();
            Console.WriteLine("Listening... Press enter to stop");
            _listener.BeginGetContext(ListenerCallback, null);
            Console.ReadLine();

            // Destroy bots
            Destroy();
        }

        private static void Build()
        {
            _brawrdonBot = new BrawrdonBot(_client, Api.ConsumerKey, Api.OauthToken, Api.ConsumerKeySecret, Api.OauthTokenSecret);
        }

        private static void Destroy()
        {
            var brawrdonBotOnline = _brawrdonBot.SetOnlineStatus(false).Result;
        }


        private static void ListenerCallback(IAsyncResult ar)
        {
            var context = _listener.EndGetContext(ar);
            var request = context.Request;
            var requestUrl = request.RawUrl.ToLower();
            var response = context.Response;
            var responseMessage = new JObject(new JProperty("status", 400), new JProperty("reason", "Invalid request"));

            // Appends the URL with a backslash to ensure parsing is done correctly
            if (!requestUrl.EndsWith("/"))
                requestUrl += "/";

            // Start listening for other requests
            _listener.BeginGetContext(ListenerCallback, null);

            // Process the request
            if (request.HttpMethod.ToUpper().Equals("POST") && requestUrl.StartsWith("/twitter"))
            {
                if (request.ContentType != null && request.ContentType.Equals("application/json"))
                    responseMessage = ProcessRequest(request, requestUrl);
            }
            else
            {
                responseMessage["status"] = 405;
                responseMessage["reason"] = "Method not allowed";
            }

            response.StatusCode = (int) responseMessage["status"];
            response.StatusDescription = (string) responseMessage["reason"];
            response.Close();
        }

        private static JObject ProcessRequest(HttpListenerRequest request, string requestUrl)
        {
            var responseMessage = new JObject(new JProperty("status", 400), new JProperty("reason", "Invalid request"));

            // Removes /twitter from the url request to easily check what kind of request this is
            requestUrl = requestUrl.Remove(0, 9);

            // Check that the request is for twitter/post/brawrdonbot/
            if (!requestUrl.StartsWith("post/"))
                return responseMessage;

            requestUrl = requestUrl.Remove(0, 5);

            if (!requestUrl.Equals("brawrdonbot/"))
                return responseMessage;

            using (var reader = new StreamReader(request.InputStream))
            {
                var requestBody = JObject.Parse(reader.ReadToEnd());

                if (requestBody["message"] != null)
                    responseMessage = _brawrdonBot.PostTweet(requestBody["message"].ToString()).Result;
                else
                    responseMessage["reason"] = "Invalid JSON";
            }

            return responseMessage;
        }
    }
}