﻿using Doctrina.ExperienceApi.Client.Http;
using Doctrina.ExperienceApi.Client.Http.Headers;
using Doctrina.ExperienceApi.Data;
using Doctrina.ExperienceApi.Data.Documents;
using Doctrina.ExperienceApi.Data.Json;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Doctrina.ExperienceApi.Client
{
    /// <summary>
    /// LRS Client
    /// </summary>
    public sealed class LRSClient : ILRSClient, IDisposable
    {
        public readonly HttpClient HttpClient;
        public readonly ApiVersion Version;

        /// <summary>
        /// Construct LRSClient with basic auth and specific version
        /// </summary>
        public LRSClient(string endpoint, string username, string password, ApiVersion version = null)
            : this(new BasicAuthHeaderValue(username, password), version)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentException("Enpoint is null or empty.", nameof(endpoint));
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Username is null or empty.", nameof(username));
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Password is null or empty.", nameof(password));
            }

            HttpClient.BaseAddress = new Uri(endpoint.EnsureEndsWith("/"));
            HttpClient.DefaultRequestHeaders.Add(ApiHeaders.XExperienceApiVersion, version.ToString());
            HttpClient.DefaultRequestHeaders.Authorization = new BasicAuthHeaderValue(username, password);
        }

        /// <summary>
        /// Construct LRSClient with custom http client, auth header and specific version
        /// </summary>
        /// <param name="httpClient"></param>
        /// <param name="authenticationHeader"></param>
        /// <param name="version"></param>
        public LRSClient(AuthenticationHeaderValue authenticationHeader, ApiVersion version = null, HttpClient httpClient = null, CultureInfo culture = null)
        {
            if (authenticationHeader is null)
            {
                throw new ArgumentNullException(nameof(authenticationHeader));
            }

            if (httpClient != null)
            {
                if (httpClient.BaseAddress == null)
                {
                    throw new ArgumentNullException("httpClient.BaseAddress", "BaseAddress must be set.");
                }

                HttpClient = httpClient;
            }
            else
            {
                HttpClient = new HttpClient();
            }

            if (!HttpClient.BaseAddress.ToString().EndsWith("/"))
            {
                HttpClient.BaseAddress = new Uri(HttpClient.BaseAddress.ToString() + "/");
            }

            Version = version;
            HttpClient.DefaultRequestHeaders.Add(ApiHeaders.XExperienceApiVersion, version.ToString());
            HttpClient.DefaultRequestHeaders.Authorization = authenticationHeader;
            HttpClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(culture?.Name ?? CultureInfo.CurrentCulture.Name));
        }

        /// <summary>
        /// Get information about remote LRS
        /// </summary>
        /// <returns></returns>
        public async Task<About> GetAbout(CancellationToken cancellation = default)
        {
            var response = await HttpClient.GetAsync("about", cancellation);

            response.EnsureSuccessStatusCode();

            JsonString jsonString = await response.Content.ReadAsStringAsync();

            return jsonString.Deserialize<About>();
        }

        #region Statements
        /// <summary>
        /// Query LRS for statements
        /// </summary>
        /// <param name="query">Statements query</param>
        /// <returns></returns>
        public async Task<StatementsResult> QueryStatements(StatementsQuery query, CancellationToken cancellationToken = default)
        {
            var parameters = query.ToParameterMap(Version);

            var uriBuilder = new UriBuilder(HttpClient.BaseAddress);
            uriBuilder.Path += "statements";
            uriBuilder.Query = parameters.ToString();
            var response = await HttpClient.GetAsync(uriBuilder.Uri, cancellationToken);

            response.EnsureSuccessStatusCode();

            StatementsResultContent responseContent = response.Content as StatementsResultContent;

            return await responseContent.ReadAsStatementsResultAsync(ApiVersion.GetLatest());
        }

        /// <summary>
        /// Get more statements
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public async Task<StatementsResult> MoreStatements(Iri more, CancellationToken cancellationToken = default)
        {
            if (more == null)
            {
                return null;
            }

            var requestUri = new Uri(HttpClient.BaseAddress, (Uri)more);
            var response = await HttpClient.GetAsync(requestUri, cancellationToken);

            response.EnsureSuccessStatusCode();

            StatementsResultContent responseContent = response.Content as StatementsResultContent;

            return await responseContent.ReadAsStatementsResultAsync(ApiVersion.GetLatest());
        }

        public async Task<StatementsResult> MoreStatements(StatementsResult result, CancellationToken cancellationToken = default)
        {
            return await MoreStatements((Iri)result.More);
        }

        public async Task<Statement> SaveStatement(Statement statement, CancellationToken cancellationToken = default)
        {
            var uriBuilder = new UriBuilder(HttpClient.BaseAddress);
            uriBuilder.Path += "/statements";

            var jsonContent = new StringContent(statement.ToJson(), Encoding.UTF8, MediaTypes.Application.Json);

            HttpContent requestContent = jsonContent;

            var attachmentsWithPayload = statement.Attachments.Where(x => x.Payload != null);
            if (attachmentsWithPayload.Any())
            {
                var multipart = new MultipartContent("mixed")
                {
                    jsonContent
                };

                foreach (var attachment in attachmentsWithPayload)
                {
                    multipart.Add(new AttachmentContent(attachment));
                }

                requestContent = multipart;
            }
            else
            {
                requestContent = jsonContent;
            }

            var response = await HttpClient.PostAsync(uriBuilder.Uri, requestContent, cancellationToken);

            return statement;
        }

        public async Task PutStatement(Statement statement, CancellationToken cancellationToken = default)
        {
            var uriBuilder = new UriBuilder(HttpClient.BaseAddress);
            uriBuilder.Path += "/statements";
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query.Add("statementId", statement.Id.ToString());

            var jsonContent = new StringContent(statement.ToJson(), Encoding.UTF8, MediaTypes.Application.Json);

            HttpContent requestContent = jsonContent;

            var attachmentsWithPayload = statement.Attachments?.Where(x => x.Payload != null);
            if (attachmentsWithPayload != null && attachmentsWithPayload.Any())
            {
                var multipart = new MultipartContent("mixed")
                    {
                        jsonContent
                    };

                foreach (var attachment in attachmentsWithPayload)
                {
                    multipart.Add(new AttachmentContent(attachment));
                }

                requestContent = multipart;
            }

            var response = await HttpClient.PutAsync(uriBuilder.Uri, requestContent, cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        public async Task<Statement[]> SaveStatements(Statement[] statements, CancellationToken cancellationToken = default)
        {
            var uriBuilder = new UriBuilder(HttpClient.BaseAddress);
            uriBuilder.Path += "/statements";

            var statementCollection = new StatementCollection(statements);
            var jsonContent = new StringContent(statementCollection.ToJson(), Encoding.UTF8, MediaTypes.Application.Json);

            HttpContent postContent = jsonContent;

            var attachmentsWithPayload = statements.SelectMany(s => s.Attachments.Where(x => x.Payload != null));
            if (attachmentsWithPayload.Any())
            {
                var multipartContent = new MultipartContent("mixed")
                {
                    jsonContent
                };

                foreach (var attachment in attachmentsWithPayload)
                {
                    multipartContent.Add(new AttachmentContent(attachment));
                }

                postContent = multipartContent;
            }

            var response = await HttpClient.PostAsync(uriBuilder.Uri, postContent);

            response.EnsureSuccessStatusCode();

            JsonString strResponse = await response.Content.ReadAsStringAsync();

            var ids = strResponse.Deserialize<Guid[]>();

            for (int i = 0; i < statements.Count(); i++)
            {
                statements[i].Id = ids[i];
            }

            return statements;
        }

        public async Task<Statement> GetStatement(Guid id, bool attachments = false, ResultFormat format = ResultFormat.Exact, CancellationToken cancellationToken = default)
        {
            var uriBuilder = new UriBuilder(HttpClient.BaseAddress);
            uriBuilder.Path += "statements";
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query.Add("statementId", id.ToString());
            if (attachments == true)
            {
                query.Add("attachments", "true");
            }

            query.Add("format", format.ToString());

            uriBuilder.Query = query.ToString();

            var response = await HttpClient.GetAsync(uriBuilder.Uri);

            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType;
            if (contentType.MediaType == MediaTypes.Application.Json)
            {
                string strResponse = await response.Content.ReadAsStringAsync();
                return new Statement((JsonString)strResponse);
            }
            else if (contentType.MediaType == MediaTypes.Multipart.Mixed)
            {
                var stream = await response.Content.ReadAsStreamAsync();
                var boundary = contentType.Parameters.SingleOrDefault(x => x.Name == "boundary");
                var multipart = new MultipartReader(boundary.Value, stream);
                var section = await multipart.ReadNextSectionAsync();
                int sectionIndex = 0;
                Statement statement = null;
                while (section != null)
                {
                    if (sectionIndex == 0)
                    {
                        string jsonString = await section.ReadAsStringAsync();
                        var serializer = new ApiJsonSerializer(ApiVersion.GetLatest());
                        var jsonReader = new JsonTextReader(new StringReader(jsonString));
                        statement = serializer.Deserialize<Statement>(jsonReader);
                    }
                    else
                    {
                        var attachmentSection = new MultipartAttachmentSection(section);
                        string hash = attachmentSection.XExperienceApiHash;
                        var attachment = statement.Attachments.FirstOrDefault(x => x.SHA2 == hash);
                    }

                    section = await multipart.ReadNextSectionAsync();
                    sectionIndex++;
                }

                return statement;
            }

            throw new Exception("Unsupported Content-Type response.");
        }

        /// <summary>
        /// Gets a voided statement
        /// </summary>
        /// <param name="id">Id of the voided statement</param>
        /// <returns>A voided statement</returns>
        public async Task<Statement> GetVoidedStatement(Guid id, bool attachments = false, ResultFormat format = ResultFormat.Exact, CancellationToken cancellationToken = default)
        {
            var uriBuilder = new UriBuilder(HttpClient.BaseAddress);
            uriBuilder.Path += "/statements";
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query.Add("voidedStatementId", id.ToString());
            if (attachments == true)
            {
                query.Add("attachments", "true");
            }

            if (format != ResultFormat.Exact)
            {
                query.Add("format", ResultFormat.Exact.ToString());
            }

            var response = await HttpClient.GetAsync(uriBuilder.Uri);

            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType;
            if (contentType.MediaType == MediaTypes.Application.Json)
            {
                string strResponse = await response.Content.ReadAsStringAsync();
                return new Statement((JsonString)strResponse);
            }
            else if (contentType.MediaType == MediaTypes.Multipart.Mixed)
            {
                var stream = await response.Content.ReadAsStreamAsync();
                var boundary = contentType.Parameters.SingleOrDefault(x => x.Name == "boundary");
                var multipart = new MultipartReader(boundary.Value, stream);
                var section = await multipart.ReadNextSectionAsync();
                int sectionIndex = 0;
                Statement statement = null;
                while (section != null)
                {
                    if (sectionIndex == 0)
                    {
                        string jsonString = await section.ReadAsStringAsync();
                        var serializer = new ApiJsonSerializer(ApiVersion.GetLatest());
                        var jsonReader = new JsonTextReader(new StringReader(jsonString));
                        statement = serializer.Deserialize<Statement>(jsonReader);
                    }
                    else
                    {
                        var attachmentSection = new MultipartAttachmentSection(section);
                        string hash = attachmentSection.XExperienceApiHash;
                        var attachment = statement.Attachments.FirstOrDefault(x => x.SHA2 == hash);
                    }

                    section = await multipart.ReadNextSectionAsync();
                    sectionIndex++;
                }

                return statement;
            }

            throw new Exception("Unsupported Content-Type response.");
        }

        /// <summary>
        /// Voids a statement
        /// </summary>
        /// <param name="id">Id of the statement to void.</param>
        /// <param name="agent">Agent who voids a statement.</param>
        /// <returns>Voiding statement</returns>
        public async Task<Statement> VoidStatement(Guid id, Agent agent, CancellationToken cancellationToken = default)
        {
            var voidStatement = new Statement
            {
                Actor = agent,
                Verb = new Verb
                {
                    Id = new Iri("http://adlnet.gov/expapi/verbs/voided"),
                    Display = new LanguageMap()
                    {
                        { "en-US", "voided" }
                    }
                },
                Object = new StatementRef { Id = id }
            };

            return await SaveStatement(voidStatement, cancellationToken);
        }
        #endregion

        #region Activity State
        public async Task<Guid[]> GetStateIds(Iri activityId, Agent agent, Guid? registration = null, CancellationToken cancellationToken = default)
        {
            var builder = new UriBuilder(HttpClient.BaseAddress);
            builder.Path += "/activities/state";

            var query = HttpUtility.ParseQueryString(string.Empty);
            query.Add("activityId", activityId.ToString());
            query.Add("agent", agent.ToString());

            if (registration.HasValue)
            {
                query.Add("registration", registration.Value.ToString("o"));
            }

            builder.Query = query.ToString();

            var response = await HttpClient.GetAsync(builder.Uri, cancellationToken);

            response.EnsureSuccessStatusCode();

            JsonString strResponse = await response.Content.ReadAsStringAsync();

            return strResponse.Deserialize<Guid[]>();
        }

        public async Task<ActivityStateDocument> GetState(string stateId, Iri activityId, Agent agent, Guid? registration = null, CancellationToken cancellationToken = default)
        {
            var builder = new UriBuilder(HttpClient.BaseAddress);
            builder.Path += "/activities/state";

            var query = HttpUtility.ParseQueryString(string.Empty);
            query.Add("stateId", stateId);
            query.Add("activityId", activityId.ToString());
            query.Add("agent", agent.ToJson());

            if (registration.HasValue)
            {
                query.Add("registration", registration.Value.ToString("o"));
            }

            builder.Query = query.ToString();

            var response = await HttpClient.GetAsync(builder.Uri, cancellationToken);

            response.EnsureSuccessStatusCode();

            var state = new ActivityStateDocument
            {
                Content = await response.Content.ReadAsByteArrayAsync(),
                ContentType = response.Content.Headers.ContentType.ToString(),
                Activity = new Activity() { Id = activityId },
                Agent = agent,
                Tag = response.Headers.ETag.Tag,
                LastModified = response.Content.Headers.LastModified
            };

            return state;
        }

        public async Task SaveState(ActivityStateDocument state, ETagMatch? matchType = null, CancellationToken cancellationToken = default)
        {
            var builder = new UriBuilder(HttpClient.BaseAddress);
            builder.Path += "/activities/state";

            var query = HttpUtility.ParseQueryString(string.Empty);
            query.Add("stateId", state.StateId);
            query.Add("activityId", state.Activity.Id.ToString());
            query.Add("agent", state.Agent.ToString());

            if (state.Registration.HasValue)
            {
                query.Add("registration", state.Registration.Value.ToString("o"));
            }

            builder.Query = query.ToString();

            var request = new HttpRequestMessage(HttpMethod.Delete, builder.Uri);

            // TOOD: Concurrency
            if (matchType.HasValue)
            {
                if (state.Tag == null)
                {
                    throw new NullReferenceException("ETag");
                }

                switch (matchType.Value)
                {
                    case ETagMatch.IfMatch:
                        request.Headers.IfMatch.Add(new EntityTagHeaderValue(state.Tag));
                        break;
                    case ETagMatch.IfNoneMatch:
                        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(state.Tag));
                        break;
                }
            }

            request.Content = new ByteArrayContent(state.Content);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(state.ContentType);

            var response = await HttpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(response.ReasonPhrase);
            }
        }

        public async Task DeleteState(ActivityStateDocument state, ETagMatch? matchType = null, CancellationToken cancellationToken = default)
        {
            var builder = new UriBuilder(HttpClient.BaseAddress);
            builder.Path += "/activities/state";

            var query = HttpUtility.ParseQueryString(string.Empty);
            query.Add("stateId", state.StateId);
            query.Add("activityId", state.Activity.Id.ToString());
            query.Add("agent", state.Agent.ToString());

            if (state.Registration.HasValue)
            {
                query.Add("registration", state.Registration.Value.ToString("o"));
            }

            builder.Query = query.ToString();

            var request = new HttpRequestMessage(HttpMethod.Delete, builder.Uri);

            // TOOD: Concurrency
            if (matchType.HasValue)
            {
                if (state.Tag == null)
                {
                    throw new NullReferenceException("ETag");
                }

                switch (matchType.Value)
                {
                    case ETagMatch.IfMatch:
                        request.Headers.IfMatch.Add(new EntityTagHeaderValue(state.Tag));
                        break;
                    case ETagMatch.IfNoneMatch:
                        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(state.Tag));
                        break;
                }
            }

            var response = await HttpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(response.ReasonPhrase);
            }
        }

        public async Task ClearState(Iri activityId, Agent agent, Guid? registration = null, ETagMatch? matchType = null, CancellationToken cancellationToken = default)
        {
            var builder = new UriBuilder(HttpClient.BaseAddress);
            builder.Path += "/activities/state";

            var query = HttpUtility.ParseQueryString(string.Empty);
            query.Add("activityId", activityId.ToString());
            query.Add("agent", agent.ToString());

            if (registration.HasValue)
            {
                query.Add("registration", registration.Value.ToString("o"));
            }

            builder.Query = query.ToString();

            var request = new HttpRequestMessage(HttpMethod.Delete, builder.Uri);

            // TOOD: Concurrency
            //if (matchType.HasValue)
            //{
            //    if (profile.ETag == null)
            //        throw new NullReferenceException("ETag");

            //    switch (matchType.Value)
            //    {
            //        case ETagMatchType.IfMatch:
            //            request.Headers.IfMatch.Add(profile.ETag);
            //            break;
            //        case ETagMatchType.IfNoneMatch:
            //            request.Headers.IfNoneMatch.Add(profile.ETag);
            //            break;
            //    }
            //}

            var response = await HttpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(response.ReasonPhrase);
            }
        }
        #endregion

        #region ActivityProfile
        public async Task<Guid[]> GetActivityProfileIds(Iri activityId, DateTimeOffset? since = null, CancellationToken cancellationToken = default)
        {

            var builder = new UriBuilder(HttpClient.BaseAddress);
            builder.Path += "/activities/profile";

            var query = HttpUtility.ParseQueryString(string.Empty);
            query.Add("activityId", activityId.ToString());
            if (since.HasValue)
            {
                query.Add("since", since.Value.ToString("o"));
            }

            builder.Query = query.ToString();

            var response = await HttpClient.GetAsync(builder.Uri, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(response.ReasonPhrase);
            }

            JsonString strResponse = await response.Content.ReadAsStringAsync();

            return strResponse.Deserialize<Guid[]>();
        }

        public async Task<ActivityProfileDocument> GetActivityProfile(string profileId, Iri activityId, CancellationToken cancellationToken = default)
        {
            var builder = new UriBuilder(HttpClient.BaseAddress);
            builder.Path += "/activities/profile";

            var query = HttpUtility.ParseQueryString(string.Empty);
            query.Add("profileId", profileId);
            query.Add("activityId", activityId.ToString());

            builder.Query = query.ToString();

            var response = await HttpClient.GetAsync(builder.Uri, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(response.ReasonPhrase);
            }

            var profile = new ActivityProfileDocument
            {
                ProfileId = profileId,
                ActivityId = activityId,
                Content = await response.Content.ReadAsByteArrayAsync(),
                ContentType = response.Content.Headers.ContentType.ToString(),
                Tag = response.Headers.ETag.ToString(),
                LastModified = response.Content.Headers.LastModified
            };
            return profile;
        }

        public async Task SaveActivityProfile(ActivityProfileDocument profile, ETagMatch? matchType = null, CancellationToken cancellationToken = default)
        {
            var builder = new UriBuilder(HttpClient.BaseAddress);
            builder.Path += "/activities/profile";

            var query = HttpUtility.ParseQueryString(string.Empty);
            query.Add("profileId", profile.ProfileId);
            query.Add("activityId", profile.ActivityId.ToString());

            if (profile.Registration.HasValue)
            {
                query.Add("registration", profile.Registration.Value.ToString("o"));
            }

            builder.Query = query.ToString();

            var request = new HttpRequestMessage(HttpMethod.Post, builder.Uri);

            if (matchType.HasValue)
            {
                if (profile.Tag == null)
                {
                    throw new NullReferenceException("ETag");
                }

                switch (matchType.Value)
                {
                    case ETagMatch.IfMatch:
                        request.Headers.IfMatch.Add(new EntityTagHeaderValue(profile.Tag));
                        break;
                    case ETagMatch.IfNoneMatch:
                        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(profile.Tag));
                        break;
                }
            }

            request.Content = new ByteArrayContent(profile.Content);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(profile.ContentType);

            var response = await HttpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(response.ReasonPhrase);
            }
        }

        public async Task DeleteActivityProfile(ActivityProfileDocument profile, ETagMatch? matchType = null, CancellationToken cancellationToken = default)
        {
            var builder = new UriBuilder(HttpClient.BaseAddress);
            builder.Path += "/activities/profile";

            var query = HttpUtility.ParseQueryString(string.Empty);
            query.Add("profileId", profile.ProfileId);
            query.Add("activityId", profile.ActivityId.ToString());

            builder.Query = query.ToString();

            var request = new HttpRequestMessage(HttpMethod.Delete, builder.Uri);

            if (matchType.HasValue)
            {
                if (profile.Tag == null)
                {
                    throw new NullReferenceException("ETag");
                }

                switch (matchType.Value)
                {
                    case ETagMatch.IfMatch:
                        request.Headers.IfMatch.Add(new EntityTagHeaderValue(profile.Tag));
                        break;
                    case ETagMatch.IfNoneMatch:
                        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(profile.Tag));
                        break;
                }
            }

            var response = await HttpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(response.ReasonPhrase);
            }
        }
        #endregion

        #region Agent Profiles
        public async Task<Guid[]> GetAgentProfileIds(Agent agent, CancellationToken cancellationToken = default)
        {
            var builder = new UriBuilder(HttpClient.BaseAddress);
            builder.Path += "/agents/profile";

            var query = HttpUtility.ParseQueryString(string.Empty);
            query.Add("agent", agent.ToString());

            builder.Query = query.ToString();

            var response = await HttpClient.GetAsync(builder.Uri, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(response.ReasonPhrase);
            }

            JsonString strResponse = await response.Content.ReadAsStringAsync();

            return strResponse.Deserialize<Guid[]>();
        }

        public async Task<AgentProfileDocument> GetAgentProfile(string profileId, Agent agent, CancellationToken cancellationToken = default)
        {
            var builder = new UriBuilder(HttpClient.BaseAddress);
            builder.Path += "/agents/profile";

            var query = HttpUtility.ParseQueryString(string.Empty);
            query.Add("profileId", profileId);
            query.Add("agent", agent.ToString());

            builder.Query = query.ToString();

            var response = await HttpClient.GetAsync(builder.Uri, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(response.ReasonPhrase);
            }

            var profile = new AgentProfileDocument
            {
                ProfileId = profileId,
                Agent = agent,
                Content = await response.Content.ReadAsByteArrayAsync(),
                ContentType = response.Content.Headers.ContentType.ToString(),
                Tag = response.Headers.ETag.ToString(),
                LastModified = response.Content.Headers.LastModified
            };
            return profile;
        }

        public async Task SaveAgentProfile(AgentProfileDocument profile, ETagMatch? matchType = null, CancellationToken cancellationToken = default)
        {
            var builder = new UriBuilder(HttpClient.BaseAddress);
            builder.Path += "/agents/profile";

            var query = HttpUtility.ParseQueryString(string.Empty);
            query.Add("profileId", profile.ProfileId);
            query.Add("agent", profile.Agent.ToString());

            builder.Query = query.ToString();

            var request = new HttpRequestMessage(HttpMethod.Post, builder.Uri);

            if (matchType.HasValue)
            {
                if (profile.Tag == null)
                {
                    throw new NullReferenceException("ETag");
                }

                switch (matchType.Value)
                {
                    case ETagMatch.IfMatch:
                        request.Headers.IfMatch.Add(new EntityTagHeaderValue(profile.Tag));
                        break;
                    case ETagMatch.IfNoneMatch:
                        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(profile.Tag));
                        break;
                }
            }

            var response = await HttpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(response.ReasonPhrase);
            }
        }

        public async Task DeleteAgentProfile(AgentProfileDocument profile, ETagMatch? matchType = null, CancellationToken cancellationToken = default)
        {
            var builder = new UriBuilder(HttpClient.BaseAddress);
            builder.Path += "/agents/profile";

            var query = HttpUtility.ParseQueryString(string.Empty);
            query.Add("profileId", profile.ProfileId);
            query.Add("agent", profile.Agent.ToString());

            builder.Query = query.ToString();

            var request = new HttpRequestMessage(HttpMethod.Delete, builder.Uri);

            if (matchType.HasValue)
            {
                if (profile.Tag == null)
                {
                    throw new NullReferenceException("ETag");
                }

                switch (matchType.Value)
                {
                    case ETagMatch.IfMatch:
                        request.Headers.IfMatch.Add(new EntityTagHeaderValue(profile.Tag));
                        break;
                    case ETagMatch.IfNoneMatch:
                        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(profile.Tag));
                        break;
                }
            }

            var response = await HttpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(response.ReasonPhrase);
            }
        }
        #endregion

        public void Dispose()
        {
            HttpClient.Dispose();
        }
    }
}
