using Open.IO;
using Open.Net.Http;
using Open.OAuth;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Open.Flickr
{
    public class FlickrClient : OAuthClient
    {
        #region ** fields

        private static string _requestTokenUri = "https://www.flickr.com/services/oauth/request_token";
        private static string _authorizeUri = "https://secure.flickr.com/services/oauth/authorize?oauth_token={0}&perms={1}";
        private static string _accessTokenUri = "https://www.flickr.com/services/oauth/access_token";
        private static readonly string _apiServiceUri = "https://api.flickr.com/services/rest";
        private static readonly string _apiUploadUri = "https://api.flickr.com/services/upload";
        private string _oauthConsumerKey;
        private string _oauthConsumerKeySecret;
        private string _accessToken;
        private string _accessTokenSecret;

        #endregion

        #region ** initialization

        public FlickrClient(string oauthConsumerKey, string oauthConsumerKeySecret, string accessToken, string accessTokenSecret)
        {
            _oauthConsumerKey = oauthConsumerKey;
            _oauthConsumerKeySecret = oauthConsumerKeySecret;
            _accessToken = accessToken;
            _accessTokenSecret = accessTokenSecret;
        }

        #endregion

        #region ** authentication

        public static async Task<OAuthToken> GetRequestTokenAsync(string oauthConsumerKey, string oauthConsumerSecret, string callbackUrl = "oob")
        {
            return await OAuthClient.GetRequestTokenAsync(_requestTokenUri, oauthConsumerKey, oauthConsumerSecret, callbackUrl);
        }

        public static new string GetAuthorizeUrl(string oauthConsumerKey, string oauthConsumerSecret, string requestToken, string perms = "read")
        {
            return string.Format(_authorizeUri, requestToken, perms);
        }

        public static async Task<OAuthToken> GetAccessTokenAsync(string oauthConsumerKey, string oauthConsumerSecret, string oauthToken, string oauthTokenSecret, string oauthVerifier)
        {
            return await OAuthClient.GetAccessTokenAsync(_accessTokenUri, oauthConsumerKey, oauthConsumerSecret, oauthToken, oauthTokenSecret, oauthVerifier);
        }

        #endregion

        #region ** public methods

        public async Task<User> GetUserInfoAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var uri = BuildApiUri("flickr.test.login");
            var client = CreateClient();
            var response = await client.GetAsync(uri, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadJsonAsync<UserResponse>();
                if (result.Stat == "ok")
                    return result.User;
                else
                    throw new FlickrException(result.Code, result.Message);
            }
            else
            {
                throw await ProcessException(response.Content);
            }
        }

        public async Task<Photo2> GetPhotoAsync(string photoId, CancellationToken cancellationToken = default(CancellationToken))
        {
            var parameters = new Dictionary<string, string>
            {
                { "photo_id", photoId },
            };

            var uri = BuildApiUri("flickr.photos.getInfo", parameters: parameters);
            var client = CreateClient();
            var response = await client.GetAsync(uri, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                PhotoResult result = await response.Content.ReadJsonAsync<PhotoResult>();

                if (result.Stat == "ok")
                {
                    return result.Photo;
                }
            }

            throw await ProcessException(response.Content);
        }

        public async Task<PhotoSizeCollection> GetPhotoSizesAsync(string photoId, CancellationToken cancellationToken = default(CancellationToken))
        {
            var parameters = new Dictionary<string, string>
            {
                { "photo_id", photoId },
            };

            var uri = BuildApiUri("flickr.photos.getSizes", parameters: parameters);
            var client = CreateClient();
            var response = await client.GetAsync(uri, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                PhotoSizeResult result = await response.Content.ReadJsonAsync<PhotoSizeResult>();

                if (result.Stat == "ok")
                {
                    return result.Sizes;
                }
            }

            throw await ProcessException(response.Content);
        }

        public async Task<Photosets> GetPhotosetsAsync(int page, int perPage, CancellationToken cancellationToken = default(CancellationToken))
        {
            var parameters = new Dictionary<string, string>
            {
                { "per_page", perPage.ToString() },
                { "page", page.ToString() },
            };

            var uri = BuildApiUri("flickr.photosets.getList", parameters: parameters);
            var client = CreateClient();
            var response = await client.GetAsync(uri, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return (await response.Content.ReadJsonAsync<PhotosetsResult>()).photosets;
            }
            else
            {
                throw await ProcessException(response.Content);
            }
        }

        public async Task<Photoset2> GetPhotosAsync(string photosetId, string extras = null, int? page = null, int? perPage = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var parameters = new Dictionary<string, string>
            {
                { "photoset_id", photosetId },
            };
            if (!string.IsNullOrWhiteSpace(extras))
                parameters.Add("extras", extras);
            if (perPage.HasValue)
                parameters.Add("per_page", perPage.ToString());
            if (page.HasValue)
                parameters.Add("page", page.ToString());

            var uri = BuildApiUri("flickr.photosets.getPhotos", parameters: parameters);
            var client = CreateClient();
            var response = await client.GetAsync(uri, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return (await response.Content.ReadJsonAsync<PhotosetResult>()).photoset;
            }
            else
            {
                throw await ProcessException(response.Content);
            }
        }

        public async Task<Photoset> GetUserPhotosAsync(string userId, string query = null, string extras = null, int? page = null, int? perPage = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var parameters = new Dictionary<string, string>
            {
                { "user_id", userId },
            };
            if (!string.IsNullOrWhiteSpace(extras))
                parameters.Add("extras", extras);
            if (perPage.HasValue)
                parameters.Add("per_page", perPage.ToString());
            if (page.HasValue)
                parameters.Add("page", page.ToString());
            if (!string.IsNullOrWhiteSpace(query))
                parameters.Add("text", query);
            var uri = BuildApiUri("flickr.photos.search", parameters: parameters);

            var client = CreateClient();
            var response = await client.GetAsync(uri, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return (await response.Content.ReadJsonAsync<PhotosResult>()).Photos;
            }
            else
            {
                throw await ProcessException(response.Content);
            }
        }

        public async Task<Comments> GetPhotosCommentsAsync(string photoId, string minCommentDate, string maxCommentDate, int offset, int limit, CancellationToken cancellationToken)
        {
            var parameters = new Dictionary<string, string>
            {
                { "photo_id", photoId },
            };
            if (minCommentDate != null)
                parameters.Add("min_comment_date", minCommentDate);
            if (maxCommentDate != null)
                parameters.Add("max_comment_date", maxCommentDate);

            var uri = BuildApiUri("flickr.photos.comments.getList", parameters: parameters);
            var client = CreateClient();
            var response = await client.GetAsync(uri, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return (await response.Content.ReadJsonAsync<CommentsResult>()).Comments;
            }
            else
            {
                throw await ProcessException(response.Content);
            }
        }

        public async Task<Comment> AddCommentAsync(string photoId, string commentText, CancellationToken cancellationToken = default(CancellationToken))
        {
            var parameters = new Dictionary<string, string>
            {
                { "photo_id", photoId },
                { "comment_text", commentText },
            };

            var uri = BuildApiUri("flickr.photos.comments.addComment", mode: "POST", parameters: parameters);
            var client = CreateClient();
            var response = await client.PostAsync(uri, new StreamContent(new MemoryStream()), cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return (await response.Content.ReadJsonAsync<CommentResult>()).Comment;
            }
            else
            {
                throw await ProcessException(response.Content);
            }
        }

        public async Task<Photoset> CreateAlbumAsync(string title, CancellationToken cancellationToken = default(CancellationToken))
        {
            var parameters = new Dictionary<string, string>
                {
                  { "title", title},
                };
            var uri = BuildApiUri("flickr.photosets.create", mode: "POST", parameters: parameters);
            var client = CreateClient();
            var response = await client.PostAsync(uri, new StreamContent(new MemoryStream()), cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadJsonAsync<Photoset>();
            }
            else
            {
                throw await ProcessException(response.Content);
            }
        }
        public async Task<Stream> DownloadPhotoAsync(Uri uri, CancellationToken cancellationToken)
        {
            var client = CreateClient();
            var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new StreamWithLength(await response.Content.ReadAsStreamAsync(), response.Content.Headers.ContentLength);
            }
            else
            {
                throw await ProcessException(response.Content);
            }
        }

        public async Task<Photo> UploadPhotoAsync(Photo file, Stream fileStream, IProgress<StreamProgress> progress, CancellationToken cancellationToken)
        {
            var parameters = new Dictionary<string, string>
            {
                { "title", file.Title },
                { "is_public", file.IsPublic.ToString() },
                { "is_friend", file.IsFriend.ToString() },
                { "is_family", file.IsFamily.ToString() },
            };
            if (!string.IsNullOrWhiteSpace(file.Description))
            {
                parameters.Add("description", file.Description);
            }
            if (!string.IsNullOrWhiteSpace(file.SafetyLevel))
            {
                parameters.Add("safety_level", file.SafetyLevel);
            }
            if (!string.IsNullOrWhiteSpace(file.ContentType))
            {
                parameters.Add("content_type", file.ContentType);
            }
            if (!string.IsNullOrWhiteSpace(file.Hidden))
            {
                parameters.Add("hidden", file.Hidden);
            }

            string authorizationheader;
            var uri = BuildUploadApiUri(out authorizationheader, parameters);
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("OAuth", authorizationheader);
            var content = new MultipartFormDataContent();
            content.Add(new StringContent(file.Title), "title");
            content.Add(new StringContent(file.IsPublic.ToString()), "is_public");
            content.Add(new StringContent(file.IsFriend.ToString()), "is_friend");
            content.Add(new StringContent(file.IsFamily.ToString()), "is_family");
            if (!string.IsNullOrWhiteSpace(file.Description))
            {
                content.Add(new StringContent(file.Description), "description");
            }
            if (!string.IsNullOrWhiteSpace(file.SafetyLevel))
            {
                content.Add(new StringContent(file.SafetyLevel), "safety_level");
            }
            if (!string.IsNullOrWhiteSpace(file.ContentType))
            {
                content.Add(new StringContent(file.ContentType), "content_type");
            }
            if (!string.IsNullOrWhiteSpace(file.Hidden))
            {
                content.Add(new StringContent(file.Hidden), "hidden");
            }
            if (string.IsNullOrWhiteSpace(file.Title))
                content.Add(new StreamedContent(fileStream, progress, cancellationToken), "photo");
            else
                content.Add(new StreamedContent(fileStream, progress, cancellationToken), "photo", file.Title);
            var response = await client.PostAsync(uri, content, cancellationToken);//.AsTask(cancellationToken, progress);
            if (response.IsSuccessStatusCode)
            {
                var sr = new System.IO.StreamReader(await response.Content.ReadAsStreamAsync());
                var responseString = sr.ReadToEnd();
                int fromPos = responseString.IndexOf("<photoid>");
                int toPos = responseString.IndexOf("</photoid>");
                if (fromPos >= 0 && toPos >= 0)
                {
                    fromPos += 9;
                    var id = responseString.Substring(fromPos, toPos - fromPos);
                    return new Photo() { Id = id };
                }
                else
                {
                    throw new Exception(responseString);
                }
            }
            else
            {
                throw await ProcessException(response.Content);
            }
        }

        public async Task UpdatePhotoAsync(string photoId, string title, string description = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var parameters = new Dictionary<string, string>
                {
                  { "photo_id", photoId },
                  { "title", title },
                };
            if (description != null)
            {
                parameters.Add("description", description);
            }
            var uri = BuildApiUri("flickr.photos.setMeta", mode: "POST", parameters: parameters);
            var client = CreateClient();
            var response = await client.PostAsync(uri, new StreamContent(new MemoryStream()), cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var responseText = await response.Content.ReadAsStringAsync();
                if (responseText.Contains("fail"))
                {
                    throw new Exception(responseText);
                }
            }
            else
            {
                throw await ProcessException(response.Content);
            }
        }

        public async Task UpdateCommentAsync(string commentId, string commentText, CancellationToken cancellationToken = default(CancellationToken))
        {
            var parameters = new Dictionary<string, string>
                {
                  { "photo_id", commentId },
                  { "comment_text", commentText },
                };
            var uri = BuildApiUri("flickr.photos.comments.editComment", mode: "POST", parameters: parameters);
            var client = CreateClient();
            var response = await client.PostAsync(uri, new StreamContent(new MemoryStream()), cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var responseText = await response.Content.ReadAsStringAsync();
                if (responseText.Contains("fail"))
                {
                    throw new Exception(responseText);
                }
            }
            else
            {
                throw await ProcessException(response.Content);
            }
        }

        public async Task DeletePhotosetAsync(string photosetId, CancellationToken cancellationToken = default(CancellationToken))
        {
            var parameters = new Dictionary<string, string>
                {
                  { "photoset_id", photosetId},
                };
            var uri = BuildApiUri("flickr.photosets.delete", mode: "POST", parameters: parameters);
            var client = CreateClient();
            var response = await client.PostAsync(uri, new StreamContent(new MemoryStream()), cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var responseText = await response.Content.ReadAsStringAsync();
                if (responseText.Contains("fail"))
                {
                    throw new Exception(responseText);
                }
            }
            else
            {
                throw await ProcessException(response.Content);
            }
        }

        public async Task DeletePhotoAsync(string photoId, CancellationToken cancellationToken = default(CancellationToken))
        {
            var parameters = new Dictionary<string, string>
                {
                  { "photo_id", photoId },
                };
            var uri = BuildApiUri("flickr.photos.delete", mode: "POST", parameters: parameters);
            var client = CreateClient();
            var response = await client.PostAsync(uri, new StreamContent(new MemoryStream()), cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var responseText = await response.Content.ReadAsStringAsync();
                if (responseText.Contains("fail"))
                {
                    throw new Exception(responseText);
                }
            }
            else
            {
                throw await ProcessException(response.Content);
            }
        }

        public async Task DeleteCommentAsync(string commentId, CancellationToken cancellationToken = default(CancellationToken))
        {
            var parameters = new Dictionary<string, string>
                {
                  { "comment_id", commentId },
                };
            var uri = BuildApiUri("flickr.photos.comments.deleteComment", mode: "POST", parameters: parameters);
            var client = CreateClient();
            var response = await client.PostAsync(uri, new StreamContent(new MemoryStream()), cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var responseText = await response.Content.ReadAsStringAsync();
                if (responseText.Contains("fail"))
                {
                    throw new Exception(responseText);
                }
            }
            else
            {
                throw await ProcessException(response.Content);
            }
        }

        public async Task RemovePhotoFromPhotosetAsync(string photosetId, string photoId, CancellationToken cancellationToken = default(CancellationToken))
        {
            var parameters = new Dictionary<string, string>
                {
                  { "photoset_id", photosetId },
                  { "photo_id ", photoId },
                };
            var uri = BuildApiUri("flickr.photosets.removePhoto", mode: "POST", parameters: parameters);
            var client = CreateClient();
            var response = await client.PostAsync(uri, new StreamContent(new MemoryStream()), cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var responseText = await response.Content.ReadAsStringAsync();
                if (responseText.Contains("fail"))
                {
                    throw new Exception(responseText);
                }
            }
            else
            {
                throw await ProcessException(response.Content);
            }
        }

        public async Task AddPhotoToPhotosetAsync(string photosetId, string photoId, CancellationToken cancellationToken = default(CancellationToken))
        {
            var parameters = new Dictionary<string, string>
                {
                  { "photoset_id", photosetId },
                  { "photo_id ", photoId },
                };
            var uri = BuildApiUri("flickr.photosets.addPhoto", mode: "POST", parameters: parameters);
            var client = CreateClient();
            var response = await client.PostAsync(uri, new StreamContent(new MemoryStream()), cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var responseText = await response.Content.ReadAsStringAsync();
                if (responseText.Contains("fail"))
                {
                    throw new Exception(responseText);
                }
            }
            else
            {
                throw await ProcessException(response.Content);
            }
        }

        #endregion

        #region ** implementation

        private Uri BuildUploadApiUri(out string authorizationheader, Dictionary<string, string> parameters = null)
        {
            authorizationheader = OAuthClient.CreateOAuthAuthorizationToken(_apiUploadUri, _oauthConsumerKey, _oauthConsumerKeySecret, _accessToken, _accessTokenSecret, parameters: parameters, mode: "POST");
            return new Uri(_apiUploadUri);
        }

        private Uri BuildApiUri(string method, string mode = "GET", Dictionary<string, string> parameters = null)
        {
            if (parameters == null)
            {
                parameters = new Dictionary<string, string>();
            }
            parameters.Add("format", "json");
            parameters.Add("method", method);
            parameters.Add("nojsoncallback", "1");

            if (!string.IsNullOrEmpty(_oauthConsumerKey))
            {
                return new Uri(OAuthClient.CreateOAuthUrl(_apiServiceUri, _oauthConsumerKey, _oauthConsumerKeySecret, _accessToken, _accessTokenSecret, mode: mode, parameters: parameters));
            }

            parameters.Add("api_key", _accessToken);
            List<string> keys = parameters.Select(p => $"{p.Key}={p.Value}").ToList();

            return new Uri($"{_apiServiceUri}?{string.Join("&", keys)}");
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient();
            client.Timeout = Timeout.InfiniteTimeSpan;
            return client;
        }

        private async Task<Exception> ProcessException(HttpContent content)
        {
            return new FlickrException(0, await content.ReadAsStringAsync());
        }

        #endregion
    }
}
