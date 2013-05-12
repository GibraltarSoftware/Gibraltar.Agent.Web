#region File Header

// <copyright file="WebRequestSourceProvider.cs" company="Gibraltar Software Inc.">
// Gibraltar Software Inc. All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Web.Management;

#endregion

namespace Gibraltar.Agent.Web.Internal
{
    internal class WebRequestSourceProvider: IMessageSourceProvider
    {
        /// <summary>
        /// Create a message source provider from the provided web request and optionally Uri
        /// </summary>
        /// <param name="requestInformation"></param>
        /// <param name="requestUri"></param>
        public WebRequestSourceProvider(WebRequestInformation requestInformation, Uri requestUri)
        {
            if (requestInformation == null)
                return;

            //by default try to use the logical path as the class & method.
            if ((requestUri != null) && (string.IsNullOrEmpty(requestUri.AbsolutePath) == false))
            {
                string swappedPath = requestUri.AbsolutePath.Replace('.', '_'); //we can't handle inline periods because they'll cause a parsing problem.
                swappedPath = swappedPath.Replace('/', '.');
                string reformatedPath = "Web Site" + (swappedPath.StartsWith(".") ? swappedPath : "." + swappedPath);
                ClassName = reformatedPath;
            }

            FileName = requestInformation.RequestPath;
        }

        public WebRequestSourceProvider(object source)
        {
            SetSource(source);
        }

        /// <summary>
        /// Should return the simple name of the method which issued the log message.
        /// </summary>
        public string MethodName { get; private set; }

        /// <summary>
        /// Should return the full name of the class (with namespace) whose method issued the log message.
        /// </summary>
        public string ClassName { get; private set; }

        /// <summary>
        /// Should return the name of the file containing the method which issued the log message.
        /// </summary>
        public string FileName { get; private set; }

        /// <summary>
        /// Should return the line within the file at which the log message was issued.
        /// </summary>
        public int LineNumber { get; private set; }

        public void SetSource(object source)
        {
            if (source == null)
                return;

            try
            {
                ClassName = source.GetType().FullName;
            }
            catch {}
        }
    }
}