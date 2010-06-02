﻿//-----------------------------------------------------------------------
// <copyright file="WebAppAuthorizationServer.cs" company="Andrew Arnott">
//     Copyright (c) Andrew Arnott. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace DotNetOpenAuth.OAuthWrap {
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.Contracts;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;
	using DotNetOpenAuth.Messaging;
	using DotNetOpenAuth.OAuthWrap.Messages;

	public class WebAppAuthorizationServer : AuthorizationServerBase {
		/// <summary>
		/// Initializes a new instance of the <see cref="WebAppAuthorizationServer"/> class.
		/// </summary>
		/// <param name="authorizationServer">The authorization server.</param>
		public WebAppAuthorizationServer(IAuthorizationServer authorizationServer)
			: base(authorizationServer) {
			Contract.Requires<ArgumentNullException>(authorizationServer != null, "authorizationServer");
		}

		/// <summary>
		/// Reads in a client's request for the Authorization Server to obtain permission from
		/// the user to authorize the Client's access of some protected resource(s).
		/// </summary>
		/// <param name="request">The HTTP request to read from.</param>
		/// <returns>The incoming request, or null if no OAuth message was attached.</returns>
		/// <exception cref="ProtocolException">Thrown if an unexpected OAuth message is attached to the incoming request.</exception>
		public WebAppRequest ReadAuthorizationRequest(HttpRequestInfo request = null) {
			if (request == null) {
				request = this.Channel.GetRequestFromContext();
			}

			WebAppRequest message;
			this.Channel.TryReadFromRequest(request, out message);
			return message;
		}

		public void ApproveAuthorizationRequest(WebAppRequest authorizationRequest, string username, Uri callback = null) {
			Contract.Requires<ArgumentNullException>(authorizationRequest != null, "authorizationRequest");

			var response = this.PrepareApproveAuthorizationRequest(authorizationRequest, callback);
			response.AuthorizingUsername = username;
			this.Channel.Send(response);
		}

		public void RejectAuthorizationRequest(WebAppRequest authorizationRequest, Uri callback = null) {
			Contract.Requires<ArgumentNullException>(authorizationRequest != null, "authorizationRequest");

			var response = this.PrepareRejectAuthorizationRequest(authorizationRequest, callback);
			this.Channel.Send(response);
		}

		public bool TryPrepareAccessTokenResponse(out IDirectResponseProtocolMessage response)
		{
			return this.TryPrepareAccessTokenResponse(this.Channel.GetRequestFromContext(), out response);
		}

		public bool TryPrepareAccessTokenResponse(HttpRequestInfo httpRequestInfo, out IDirectResponseProtocolMessage response)
		{
			Contract.Requires<ArgumentNullException>(httpRequestInfo != null, "httpRequestInfo");

			var request = ReadAccessTokenRequest(httpRequestInfo);
			if (request != null)
			{
				// This convenience method only encrypts access tokens assuming that this auth server
				// doubles as the resource server.
				response = PrepareAccessTokenResponse(request, this.AuthorizationServer.AccessTokenSigningPrivateKey);
				return true;
			}

			response = null;
			return false;
		}

		internal WebAppFailedResponse PrepareRejectAuthorizationRequest(WebAppRequest authorizationRequest, Uri callback = null) {
			Contract.Requires<ArgumentNullException>(authorizationRequest != null, "authorizationRequest");
			Contract.Ensures(Contract.Result<WebAppFailedResponse>() != null);

			if (callback == null) {
				callback = this.GetCallback(authorizationRequest);
			}

			var response = new WebAppFailedResponse(callback, authorizationRequest);
			return response;
		}

		internal WebAppSuccessResponse PrepareApproveAuthorizationRequest(WebAppRequest authorizationRequest, Uri callback = null) {
			Contract.Requires<ArgumentNullException>(authorizationRequest != null, "authorizationRequest");
			Contract.Ensures(Contract.Result<WebAppSuccessResponse>() != null);

			if (callback == null) {
				callback = this.GetCallback(authorizationRequest);
			}

			var client = this.AuthorizationServer.GetClientOrThrow(authorizationRequest.ClientIdentifier);
			var response = new WebAppSuccessResponse(callback, authorizationRequest);
			return response;
		}

		internal WebAppAccessTokenRequest ReadAccessTokenRequest(HttpRequestInfo requestInfo = null) {
			if (requestInfo == null) {
				requestInfo = this.Channel.GetRequestFromContext();
			}

			WebAppAccessTokenRequest request;
			this.Channel.TryReadFromRequest(requestInfo, out request);
			return request;
		}

		internal AccessTokenSuccessResponse PrepareAccessTokenResponse(WebAppAccessTokenRequest request, RSAParameters resourceServerPublicKey) {
			Contract.Requires<ArgumentNullException>(request != null, "request");
			Contract.Ensures(Contract.Result<AccessTokenSuccessResponse>() != null);

			return this.OAuthChannel.PrepareAccessToken(request, resourceServerPublicKey);
		}

		protected Uri GetCallback(WebAppRequest authorizationRequest) {
			Contract.Requires<ArgumentNullException>(authorizationRequest != null, "authorizationRequest");
			Contract.Ensures(Contract.Result<Uri>() != null);

			// Prefer a request-specific callback to the pre-registered one (if any).
			if (authorizationRequest.Callback != null) {
				return authorizationRequest.Callback;
			}

			var client = this.AuthorizationServer.GetClient(authorizationRequest.ClientIdentifier);
			if (client.Callback != null) {
				return client.Callback;
			}

			throw ErrorUtilities.ThrowProtocol(OAuthWrapStrings.NoCallback);
		}
	}
}
