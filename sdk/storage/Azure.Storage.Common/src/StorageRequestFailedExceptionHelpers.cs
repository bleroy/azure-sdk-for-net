﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Azure.Core;

namespace Azure.Storage
{
    /// <summary>
    /// Provide helpful information about errors calling Azure Storage endpoints.
    /// </summary>
    internal class StorageRequestFailedExceptionHelpers
    {
        public static RequestFailedException CreateException(Response response, string message = null, Exception innerException = null, string errorCode = null, IDictionary<string, string> additionalInfo = null)
        {
            int status = response?.Status ?? throw Errors.ArgumentNull(nameof(response));
            string code = GetErrorCode(response, errorCode);

            var exception = new RequestFailedException(status,
                CreateMessage(response, message ?? response.ReasonPhrase, code, additionalInfo),
                code, innerException);

            if (additionalInfo != null)
            {
                foreach (KeyValuePair<string, string> pair in additionalInfo)
                {
                    exception.Data.Add(pair.Key, pair.Value);
                }
            }

            return exception;
        }

        private static string GetErrorCode(Response response, string errorCode)
        {
            if (string.IsNullOrEmpty(errorCode))
            {
                response.Headers.TryGetValue(Constants.HeaderNames.ErrorCode, out errorCode);
            }

            return errorCode;
        }

        /// <summary>
        /// Create the exception's Message.
        /// </summary>
        /// <param name="message">The default message.</param>
        /// <param name="response">The error response.</param>
        /// <param name="errorCode">An optional error code.</param>
        /// <param name="additionalInfo">Optional additional information.</param>
        /// <returns>The exception's Message.</returns>
        private static string CreateMessage(
            Response response,
            string message,
            string errorCode,
            IDictionary<string, string> additionalInfo)
        {
            // Start with the message, status, and reason
            StringBuilder messageBuilder = new StringBuilder()
                .AppendLine(message)
                .Append("Status: ")
                .Append(response.Status.ToString(CultureInfo.InvariantCulture))
                .Append(" (")
                .Append(response.ReasonPhrase)
                .AppendLine(")");

            // Make the Storage ErrorCode especially prominent
            if (!string.IsNullOrEmpty(errorCode) ||
                response.Headers.TryGetValue(Constants.HeaderNames.ErrorCode, out errorCode))
            {
                messageBuilder
                    .AppendLine()
                    .Append("ErrorCode: ")
                    .AppendLine(errorCode);
            }

            // A Storage error's Content is (currently) always the ErrorCode and
            // AdditionalInfo, so we skip the specific Content section
            if (additionalInfo != null && additionalInfo.Count > 0)
            {
                messageBuilder
                    .AppendLine()
                    .AppendLine("Additional Information:");
                foreach (KeyValuePair<string, string> info in additionalInfo)
                {
                    messageBuilder
                        .Append(info.Key)
                        .Append(": ")
                        .AppendLine(info.Value);
                }
            }

            // Include the response headers
            messageBuilder
                .AppendLine()
                .AppendLine("Headers:");
            foreach (HttpHeader responseHeader in response.Headers)
            {
                messageBuilder
                    .Append(responseHeader.Name)
                    .Append(": ")
                    .AppendLine(responseHeader.Value);
            }

            return messageBuilder.ToString();
        }
    }
}
