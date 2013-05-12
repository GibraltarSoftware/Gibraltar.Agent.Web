#region File Header

// <copyright file="HttpRequestMetric.cs" company="Gibraltar Software Inc.">
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
using System.Diagnostics;
using Gibraltar.Agent.Metrics;

#endregion

namespace Gibraltar.Agent.Web.Internal
{
    [EventMetric(GibraltarEventProvider.LogSystem, "Web Site.Requests", "Page Hit", Caption = "Page Hit", Description = "Performance tracking data about every web page hit")]
    internal class HttpRequestMetric: IDisposable
    {
        private bool m_Suppressed;

        private readonly Stopwatch m_RequestTimer;
        private Stopwatch m_AuthenticateRequestTimer;
        private Stopwatch m_AuthorizeRequestTimer;
        private Stopwatch m_ResolveRequestCacheTimer;
        private Stopwatch m_AcquireRequestStateTimer;
        private Stopwatch m_RequestHandlerExecuteTimer;
        private Stopwatch m_ReleaseRequestStateTimer;
        private Stopwatch m_UpdateRequestCacheTimer;
        private Stopwatch m_LogRequestTimer;

        public HttpRequestMetric()
        {
            m_RequestTimer = Stopwatch.StartNew();
        }

        [EventMetricValue("pageName", SummaryFunction.Count, "Hits", Caption = "Page", Description = "The page name without path")]
        public string PageName { get; set; }

        [EventMetricValue("absolutePath", SummaryFunction.Count, "Hits", Caption = "Absolute Path", 
            Description = "The full path from the root of the web site to the page that was requested including the page")]
        public string AbsolutePath { get; set; }

        [EventMetricValue("totalDuration", SummaryFunction.Average, "Milliseconds", Caption = "Total Request Duration", 
            Description = "The entire time it took for the request to be satisfied", IsDefaultValue = true)]
        public double TotalDuration { get; private set; }

        [EventMetricValue("authenticateDuration", SummaryFunction.Average, "Milliseconds", Caption = "Authenticate Request Duration", 
            Description = "The time it took for the request to be authenticated")]
        public double AuthenticateDuration { get; private set; }

        [EventMetricValue("authorizeRequestDuration", SummaryFunction.Average, "Milliseconds", Caption = "Authorize Request Duration", 
            Description = "The time it took for the request to be authorized")]
        public double AuthorizeRequestDuration { get; private set; }

        [EventMetricValue("resolveRequestCacheDuration", SummaryFunction.Average, "Milliseconds", Caption = "Resolve Request Cache Duration", 
            Description = "The time it took for the request to be looked up in cache")]
        public double ResolveRequestCacheDuration { get; private set; }

        [EventMetricValue("acquireRequestStateDuration", SummaryFunction.Average, "Milliseconds", Caption = "Acquire Request State Duration", 
            Description = "The time it took for the request state to be acquired")]
        public double AcquireRequestStateDuration { get; private set; }

        [EventMetricValue("requestHandlerExecuteDuration", SummaryFunction.Average, "Milliseconds", Caption = "Request Handler Execute Duration", 
            Description = "The time it took for the request handler to execute. This includes the time for most ASP.NET page code.")]
        public double RequestHandlerExecuteDuration { get; private set; }

        [EventMetricValue("releaseRequestStateDuration", SummaryFunction.Average, "Milliseconds", Caption = "Release Request State Duration", 
            Description = "The time it took for the request state to be released")]
        public double ReleaseRequestStateDuration { get; private set; }

        [EventMetricValue("updateRequestCacheDuration", SummaryFunction.Average, "Milliseconds", Caption = "Update Request Cache Duration", 
            Description = "The time it took for the request cache to be updated")]
        public double UpdateRequestCacheDuration { get; private set; }

        [EventMetricValue("logRequestDuration", SummaryFunction.Average, "Milliseconds", Caption = "Log Request Duration", 
            Description = "The time it took for the request to be logged")]
        public double LogRequestDuration { get; private set; }

        [EventMetricValue("servedFromCache", SummaryFunction.Average, "Hits", Caption = "Cached Response",
            Description = "Indicates if the response was served from the output cache instead of generated.")]
        public bool ServedFromCache { get; private set; }


        [EventMetricValue("queryString", SummaryFunction.Count, "Hits", Caption = "Query String",
            Description = "The query string used for the request")]
        public string QueryString { get; set; }

        /// <summary>
        /// If called prevents the metric from recording its result when disposed (likely because it wouldn't be considered valid)
        /// </summary>
        public void Suppress()
        {
            m_Suppressed = true;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            try
            {
                //Close out our duration and log the final result.  All other timers should have been stopped symmetrically.
                TotalDuration = StopAndRecordDuration(m_RequestTimer);

                //and now you can log us.
                if (m_Suppressed == false)
                    EventMetric.Write(this);

            }
            catch(Exception ex)
            {
                GC.KeepAlive(ex);

#if DEBUG
                Log.RecordException(ex, "System", true);
#endif
            }
        }

        #region Internal Properties and Methods

        internal void AuthenticateStart()
        {
            m_AuthenticateRequestTimer = Stopwatch.StartNew();
        }

        internal void AuthenticateEnd()
        {
            AuthenticateDuration = StopAndRecordDuration(m_AuthenticateRequestTimer);
        }

        internal void AuthorizeRequestStart()
        {
            m_AuthorizeRequestTimer = Stopwatch.StartNew();
        }

        internal void AuthorizeRequestEnd()
        {
            AuthorizeRequestDuration = StopAndRecordDuration(m_AuthorizeRequestTimer);
            ServedFromCache = true; //we'll set this to false when we realize it wasn't.
        }

        internal void ResolveRequestCacheStart()
        {
            m_ResolveRequestCacheTimer = Stopwatch.StartNew();
            //we set served from cache true at the end of AUTHORIZE because we won't necessarily even get this s
            //start event if the cache module is ahead of us in the module chain.
        }

        internal void ResolveRequestCacheEnd()
        {
            ResolveRequestCacheDuration = StopAndRecordDuration(m_ResolveRequestCacheTimer);
        }

        internal void AcquireRequestStateStart()
        {
            m_AcquireRequestStateTimer = Stopwatch.StartNew();
        }

        internal void AcquireRequestStateEnd()
        {
            AcquireRequestStateDuration = StopAndRecordDuration(m_AcquireRequestStateTimer);
        }

        internal void RequestHandlerExecuteStart()
        {
            m_RequestHandlerExecuteTimer = Stopwatch.StartNew();
            ServedFromCache = false;  //if we're executing the handler then it isn't coming from cache.
        }

        internal void RequestHandlerExecuteEnd()
        {
            RequestHandlerExecuteDuration = StopAndRecordDuration(m_RequestHandlerExecuteTimer);
        }

        internal void ReleaseRequestStateStart()
        {
            m_ReleaseRequestStateTimer = Stopwatch.StartNew();
        }

        internal void ReleaseRequestStateEnd()
        {
            ReleaseRequestStateDuration = StopAndRecordDuration(m_ReleaseRequestStateTimer);
        }

        internal void UpdateRequestCacheStart()
        {
            m_UpdateRequestCacheTimer = Stopwatch.StartNew();
        }

        internal void UpdateRequestCacheEnd()
        {
            UpdateRequestCacheDuration = StopAndRecordDuration(m_UpdateRequestCacheTimer);
        }

        internal void LogRequestStart()
        {
            m_LogRequestTimer = Stopwatch.StartNew();
        }

        internal void LogRequestEnd()
        {
            LogRequestDuration = StopAndRecordDuration(m_LogRequestTimer);
        }

        #endregion

        #region Private Properties and Methods

        private static double StopAndRecordDuration(Stopwatch timer)
        {
            if (timer == null)
                return 0;

            timer.Stop();
            return timer.Elapsed.TotalMilliseconds;
        }

        #endregion
    }
}