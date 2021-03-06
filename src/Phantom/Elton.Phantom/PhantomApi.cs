﻿#region License

//   Copyright 2014 Elton FAN (eltonfan@live.cn, http://elton.io)
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License. 

#endregion

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using RestSharp;
using System.Threading.Tasks;
using Elton.Phantom.Models.Version1;
using Elton.Phantom.Rest;

namespace Elton.Phantom
{
    /// <summary>
    /// 实现对幻腾API的各功能封装。
    /// </summary>
    /// <remarks>
    /// https://huantengsmart.com/doc/api_v1
    /// https://huantengsmart.com/doc/api_v2
    /// </remarks>
    public partial class PhantomApi : Elton.Phantom.Rest.ApiClient
    {
        static readonly Common.Logging.ILog log = Common.Logging.LogManager.GetLogger(typeof(PhantomApi));

        string token = null;
        public PhantomApi(PhantomConfiguration config)
            : base(config)
        { }
        
        /// <summary>
        /// Allows for extending request processing for <see cref="ApiClient"/> generated code.
        /// </summary>
        /// <param name="request">The RestSharp request object</param>
        partial void InterceptRequest(IRestRequest request);

        /// <summary>
        /// Allows for extending response processing for <see cref="ApiClient"/> generated code.
        /// </summary>
        /// <param name="request">The RestSharp request object</param>
        /// <param name="response">The RestSharp response object</param>
        partial void InterceptResponse(IRestRequest request, IRestResponse response);
        
        /// <summary>
        /// Makes the HTTP request (Sync).
        /// </summary>
        /// <param name="path">URL path.</param>
        /// <param name="method">HTTP method.</param>
        /// <param name="queryParams">Query parameters.</param>
        /// <param name="postBody">HTTP body (POST request).</param>
        /// <param name="headerParams">Header parameters.</param>
        /// <param name="formParams">Form parameters.</param>
        /// <param name="fileParams">File parameters.</param>
        /// <param name="pathParams">Path parameters.</param>
        /// <param name="contentType">Content Type of the request</param>
        /// <returns>Object</returns>
        public ApiResponse<T> CallApi<T>(int apiVersion, string path, Method method,
            IEnumerable<KeyValuePair<string, string>> queryParams = null,
            object postBody = null, string contentType = null,
            IEnumerable<KeyValuePair<string, string>> headerParams = null,
            IEnumerable<KeyValuePair<string, object>> formParams = null,
            IEnumerable<KeyValuePair<string, FileParameter>> fileParams = null,
            IEnumerable<KeyValuePair<string, object>> pathParams = null,
            ExceptionFactory check = null)
        {
            if (apiVersion < 1 || apiVersion > 2)
                throw new NotSupportedException("Only v1 & v2 api is supported.");

            var request = PrepareRequest(path, method,
                queryParams: queryParams,
                postBody: postBody, contentType: contentType,
                headerParams: headerParams,
                formParams: formParams,
                fileParams: fileParams,
                pathParams: pathParams,
                accept: $"application/vnd.huantengsmart-v{apiVersion}+json");//"application/json"

            InterceptRequest(request);
            var response = client.Execute(request);
            InterceptResponse(request, response);

            var error = CheckError(response);
            if (error == null && check != null)
                error = check(path, response);
            if (error != null)
                throw error;

            return new ApiResponse<T>((int)response.StatusCode,
                response.Headers.ToDictionary(x => x.Name, x => x.Value.ToString()),
                (T)converter.Deserialize(response, typeof(T)));
        }

        /// <summary>
        /// Makes the asynchronous HTTP request.
        /// </summary>
        /// <param name="path">URL path.</param>
        /// <param name="method">HTTP method.</param>
        /// <param name="queryParams">Query parameters.</param>
        /// <param name="postBody">HTTP body (POST request).</param>
        /// <param name="headerParams">Header parameters.</param>
        /// <param name="formParams">Form parameters.</param>
        /// <param name="fileParams">File parameters.</param>
        /// <param name="pathParams">Path parameters.</param>
        /// <param name="contentType">Content type.</param>
        /// <returns>The Task instance.</returns>
        public async Task<ApiResponse<T>> CallApiAsync<T>(int apiVersion, string path, Method method,
            IEnumerable<KeyValuePair<string, string>> queryParams = null,
            object postBody = null, string contentType = null,
            IEnumerable<KeyValuePair<string, string>> headerParams = null,
            IEnumerable<KeyValuePair<string, object>> formParams = null,
            IEnumerable<KeyValuePair<string, FileParameter>> fileParams = null,
            IEnumerable<KeyValuePair<string, object>> pathParams = null,
            ExceptionFactory check = null)
        {
            if (apiVersion < 1 || apiVersion > 2)
                throw new NotSupportedException("Only v1 & v2 api is supported.");

            var request = PrepareRequest(path, method,
                queryParams: queryParams,
                postBody: postBody, contentType: contentType,
                headerParams: headerParams,
                formParams: formParams,
                fileParams: fileParams,
                pathParams: pathParams,
                accept: $"application/vnd.huantengsmart-v{apiVersion}+json");//"application/json"

            InterceptRequest(request);
            var response = await client.ExecuteTaskAsync(request);
            InterceptResponse(request, response);

            var error = CheckError(response);
            if (error == null && check != null)
                error = check(path, response);
            if (error != null)
                throw error;

            return new ApiResponse<T>((int)response.StatusCode,
                response.Headers.ToDictionary(x => x.Name, x => x.Value.ToString()),
                (T)converter.Deserialize(response, typeof(T)));
        }

        /// <summary>
        /// 批命令请求。
        /// </summary>
        /// <param name="authorization"></param>
        /// <param name="ops"></param>
        /// <returns></returns>
        internal BatchResults Batch(int apiVersion, params Operation[] ops)
        {
            var content = new JObject();
            content.Add("ops", JToken.FromObject(ops));
            content.Add("sequential", true);

            //https://huantengsmart.com/massapi
            return this.Post<BatchResults>(apiVersion, "../massapi", content);
        }

        protected override Exception CheckError(IRestResponse response)
        {
            if (response.IsSuccessful)
                return null;

            Exception error = null;
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                case HttpStatusCode.Created:
                    break;
                case HttpStatusCode.Unauthorized:
                    error = new PhantomUnauthorizedException(response.StatusDescription);
                    break;
                default://其他错误
                    string message = "";
                    var status = PhantomExceptionStatus.Unknown;
                    if (!TryParseErrorMessage(response.Content, out status, out message))
                    {
                        message = response.Content;
                        status = PhantomExceptionStatus.Unknown;
                    }
                    error = new PhantomException(message, status);
                    break;
            }

            return error;
        }

        static bool TryParseErrorMessage(string content, out PhantomExceptionStatus status, out string message)
        {
            status = PhantomExceptionStatus.Unknown;
            message = "";

            JObject obj = JsonConvert.DeserializeObject(content) as JObject;
            if (obj == null)
                return false;

            string error = obj.Value<string>("error");
            if (string.IsNullOrEmpty(error))
                return false;
            //进一步解析错误信息
            string[] parts = (error ?? "").Split(new char[] { ':' });
            if (parts == null || parts.Length < 2 || !Enum.TryParse<PhantomExceptionStatus>(parts[0], out status))
            {
                message = error;
                status = PhantomExceptionStatus.Unknown;

                return true;
            }
            else
            {
                message = parts[1];
                return true;
            }
        }

        protected T Get<T>(int apiVersion, string url, IEnumerable<KeyValuePair<string, string>> queryParams = null, IEnumerable<KeyValuePair<string, object>> pathParams = null, ExceptionFactory check = null)
        {
            return CallApi<T>(apiVersion, url, Method.GET,
                queryParams: queryParams,
                pathParams: pathParams,
                check: check).Data;
        }

        protected async Task<T> GetAsync<T>(int apiVersion, string url, IEnumerable<KeyValuePair<string, string>> queryParams = null, IEnumerable<KeyValuePair<string, object>> pathParams = null, ExceptionFactory check = null)
        {
            var response = await CallApiAsync<T>(apiVersion, url, Method.GET,
                queryParams: queryParams,
                pathParams: pathParams,
                check: check);

            return response.Data;
        }

        internal T Post<T>(int apiVersion, string url, object postBody = null, IEnumerable<KeyValuePair<String, object>> formParams = null)
        {
            return CallApi<T>(apiVersion, url, Method.POST, 
                postBody: postBody,
                formParams: formParams).Data;
        }

        internal async Task<T> PostAsync<T>(int apiVersion, string url, object postBody = null, IEnumerable<KeyValuePair<String, object>> formParams = null)
        {
            var response = await CallApiAsync<T>(apiVersion, url, Method.POST,
                postBody: postBody,
                formParams: formParams);

            return response.Data;
        }

        protected T Put<T>(int apiVersion, string url, object postBody = null, IEnumerable<KeyValuePair<String, object>> formParams = null, ExceptionFactory check = null)
        {
            return CallApi<T>(apiVersion, url, Method.PUT,
                postBody: postBody,
                formParams: formParams,
                check: check).Data;
        }

        protected async Task<T> PutAsync<T>(int apiVersion, string url, object postBody = null, IEnumerable<KeyValuePair<String, object>> formParams = null, ExceptionFactory check = null)
        {
            var response = await CallApiAsync<T>(apiVersion, url, Method.PUT,
                postBody: postBody,
                formParams: formParams,
                check: check);

            return response.Data;
        }

        protected T Delete<T>(int apiVersion, string url, ExceptionFactory check = null)
        {
            var result = CallApi<T>(apiVersion, url, Method.DELETE,
                check: check).Data;
            return result;
        }

        protected async Task<T> DeleteAsync<T>(int apiVersion, string url, ExceptionFactory check = null)
        {
            var response = await CallApiAsync<dynamic>(apiVersion, url, Method.DELETE,
                check: check);
            return response.Data;
        }
    }
}
