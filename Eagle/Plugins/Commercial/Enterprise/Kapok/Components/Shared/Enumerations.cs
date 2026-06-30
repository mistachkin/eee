/*
 * Enumerations.cs --
 *
 * Extensible Adaptable Generalized Logic Engine (Eagle)
 * Eagle Enterprise Edition: Kapok SDK v1.0
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Text;

#if KAPOK
using Eagle._Attributes;
#else
using System.Runtime.InteropServices;
#endif

namespace Kapok.Components.Shared
{
    /// <summary>
    /// This enumeration is supposed to represent a more-or-less "complete"
    /// list of possible status codes that may be returned by an HTTP server.
    /// </summary>
#if KAPOK
    [ObjectId("a89b9231-e10e-4d08-8dcd-c4221b997fad")]
#else
    [Guid("a89b9231-e10e-4d08-8dcd-c4221b997fad")]
#endif
    public enum HttpStatusCode
    {
        /// <summary>
        /// This code represents an unknown status from an HTTP server, i.e.
        /// because the request has not been issued yet, etc.
        /// </summary>
        None = 0,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This code should never be returned by an HTTP server. and it has
        /// been permanently reserved.
        /// </summary>
        Invalid = 1,

        ///////////////////////////////////////////////////////////////////////
        // Informational 1xx
        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This interim response indicates that the client should continue the
        /// request or ignore the response if the request is already finished.
        /// </summary>
        Continue = 100,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This code is sent in response to an Upgrade request header from the
        /// client and indicates the protocol the server is switching to.
        /// </summary>
        SwitchingProtocols = 101,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This code indicates that the server has received and is processing
        /// the request, but no response is available yet.
        /// </summary>
        Processing = 102,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This status code is primarily intended to be used with the Link
        /// header, letting the user agent start preloading resources while the
        /// server prepares a response or preconnect to an origin from which
        /// the page will need resources.
        /// </summary>
        EarlyHints = 103,

        ///////////////////////////////////////////////////////////////////////
        // Successful 2xx
        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The request succeeded.  The result meaning of "success" depends on
        /// the HTTP method used.
        /// </summary>
        Ok = 200,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The request succeeded, and a new resource was created as a result.
        /// This is typically the response sent after POST requests, or some
        /// PUT requests.
        /// </summary>
        Created = 201,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The request has been received but not yet acted upon. It is
        /// noncommittal, since there is no way in HTTP to later send an
        /// asynchronous response indicating the outcome of the request.  It
        /// is intended for cases where another process or server handles the
        /// request, or for batch processing.
        /// </summary>
        Accepted = 202,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This response code means the returned metadata is not exactly the
        /// same as is available from the origin server, but is collected from
        /// a local or a third-party copy. This is mostly used for mirrors or
        /// backups of another resource. Except for that specific case, the 200
        /// OK response is preferred to this status.
        /// </summary>
        NonAuthoritativeInformation = 203,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// There is no content to send for this request, but the headers may
        /// be useful.  The user agent may update its cached headers for this
        /// resource with the new ones.
        /// </summary>
        NoContent = 204,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Tells the user agent to reset the document which sent this request.
        /// </summary>
        ResetContent = 205,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This response code is used when the Range header is sent from the
        /// client to request only part of a resource.
        /// </summary>
        PartialContent = 206,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Conveys information about multiple resources, for situations where
        /// multiple status codes might be appropriate.
        /// </summary>
        MultiStatus = 207,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Used inside a "dav:propstat" response element to avoid repeatedly
        /// enumerating the internal members of multiple bindings to the same
        /// collection.
        /// </summary>
        AlreadyReported = 208,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The server has fulfilled a GET request for the resource, and the
        /// response is a representation of the result of one or more
        /// instance-manipulations applied to the current instance.
        /// </summary>
        ImUsed = 226,

        ///////////////////////////////////////////////////////////////////////
        // Redirection 3xx
        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The request has more than one possible response. The user agent or
        /// user should choose one of them. (There is no standardized way of
        /// choosing one of the responses, but HTML links to the possibilities
        /// are recommended so the user can pick.)
        /// </summary>
        MultipleChoices = 300,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The URL of the requested resource has been changed permanently.
        /// The new URL is given in the response.
        /// </summary>
        MovedPermanently = 301,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This response code means that the URI of requested resource has
        /// been changed temporarily. Further changes in the URI might be made
        /// in the future.  Therefore, this same URI should be used by the
        /// client in future requests.
        /// </summary>
        MovedTemporarily = 302,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The server sent this response to direct the client to get the
        /// requested resource at another URI with a GET request.
        /// </summary>
        SeeOther = 303,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This is used for caching purposes. It tells the client that the
        /// response has not been modified, so the client can continue to use
        /// the same cached version of the response.
        /// </summary>
        NotModified = 304,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Defined in a previous version of the HTTP specification to indicate
        /// that a requested response must be accessed by a proxy.  It has been
        /// deprecated due to security concerns regarding in-band configuration
        /// of a proxy.
        /// </summary>
        UseProxy = 305,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This response code is no longer used; it is just reserved.  It was
        /// used in a previous version of the HTTP/1.1 specification.
        /// </summary>
        SwitchProxy = 306,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The server sends this response to direct the client to get the
        /// requested resource at another URI with the same method that was
        /// used in the prior request. This has the same semantics as the 302
        /// Found HTTP response code, with the exception that the user agent
        /// must not change the HTTP method used: if a POST was used in the
        /// first request, a POST must be used in the second request.
        /// </summary>
        TemporaryRedirect = 307,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This means that the resource is now permanently located at another
        /// URI, specified by the Location: HTTP Response header. This has the
        /// same semantics as the 301 Moved Permanently HTTP response code,
        /// with the exception that the user agent must not change the HTTP
        /// method used: if a POST was used in the first request, a POST must
        /// be used in the second request.
        /// </summary>
        PermanentRedirect = 308,

        ///////////////////////////////////////////////////////////////////////
        // Client Error 4xx
        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The server cannot or will not process the request due to something
        /// that is perceived to be a client error (e.g., malformed request
        /// syntax, invalid request message framing, or deceptive request
        /// routing).
        /// </summary>
        BadRequest = 400,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Although the HTTP standard specifies "unauthorized", semantically
        /// this response means "unauthenticated".  That is, the client must
        /// authenticate itself to get the requested response.
        /// </summary>
        Unauthorized = 401,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This response code is reserved for future use.  The initial aim for
        /// creating this code was using it for digital payment systems,
        /// however this status code is used very rarely and no standard
        /// convention exists.
        /// </summary>
        PaymentRequired = 402,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The client does not have access rights to the content; that is, it
        /// is unauthorized, so the server is refusing to give the requested
        /// resource. Unlike 401 Unauthorized, the identity of the client is
        /// known to the server.
        /// </summary>
        Forbidden = 403,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The server cannot find the requested resource. In the browser, this
        /// means the URL is not recognized.  In an API, this can also mean
        /// that the endpoint is valid but the resource itself does not exist.
        /// Servers may also send this response instead of 403 Forbidden to
        /// hide the existence of a resource from an unauthorized client.  This
        /// response code is probably the most well known due to its frequent
        /// occurrence on the web.
        /// </summary>
        NotFound = 404,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The request method is known by the server but is not supported by
        /// the target resource.  For example, an API may not allow calling
        /// DELETE to remove a resource.
        /// </summary>
        MethodNotAllowed = 405,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This response is sent when the web server, after performing
        /// server-driven content negotiation, does not find any content that
        /// conforms to the criteria given by the user agent.
        /// </summary>
        NotAcceptable = 406,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This is similar to 401 Unauthorized but authentication is needed to
        /// be done by a proxy.
        /// </summary>
        ProxyAuthenticationRequired = 407,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This response is sent on an idle connection by some servers, even
        /// without any previous request by the client.  It means that the
        /// server would like to shut down this unused connection.  This
        /// response is used much more since some browsers, like Chrome,
        /// Firefox 27+, or IE9, use HTTP pre-connection mechanisms to speed
        /// up browsing.  Also note that some servers merely shut down the
        /// connection without sending this message.
        /// </summary>
        RequestTimeout = 408,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This response is sent when a request conflicts with the current
        /// state of the server.
        /// </summary>
        Conflict = 409,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This response is sent when the requested content has been
        /// permanently deleted from server, with no forwarding address.
        /// Clients are expected to remove their caches and links to the
        /// resource.  The HTTP specification intends this status code to be
        /// used for "limited-time, promotional services".  APIs should not
        /// feel compelled to indicate resources that have been deleted with
        /// this status code.
        /// </summary>
        Gone = 410,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Server rejected the request because the Content-Length header field
        /// is not defined and the server requires it.
        /// </summary>
        LengthRequired = 411,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The client has indicated preconditions in its headers which the
        /// server does not meet.
        /// </summary>
        PreconditionFailed = 412,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Request entity is larger than limits defined by server.  The server
        /// might close the connection or return an Retry-After header field.
        /// </summary>
        PayloadTooLarge = 413,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The URI requested by the client is longer than the server is
        /// willing to interpret.
        /// </summary>
        UriTooLong = 414,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The media format of the requested data is not supported by the
        /// server, so the server is rejecting the request.
        /// </summary>
        UnsupportedMediaType = 415,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The range specified by the Range header field in the request cannot
        /// be fulfilled.  It is possible that the range is outside the size of
        /// the target URI's data.
        /// </summary>
        RangeNotSatisfiable = 416,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This response code means the expectation indicated by the Expect
        /// request header field cannot be met by the server.
        /// </summary>
        ExpectationFailed = 417,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The server refuses the attempt to brew coffee with a teapot.  Some
        /// systems use this response code to indicate that a "robot" has been
        /// detected.
        /// </summary>
        ImATeapot = 418,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The request was directed at a server that is not able to produce a
        /// response.  This can be sent by a server that is not configured to
        /// produce responses for the combination of scheme and authority that
        /// are included in the request URI.
        /// </summary>
        MisdirectedRequest = 421,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The request was well-formed but was unable to be followed due to
        /// semantic errors.
        /// </summary>
        UnprocessableEntity = 422,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The resource that is being accessed is locked.
        /// </summary>
        Locked = 423,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The request failed due to failure of a previous request.
        /// </summary>
        FailedDependency = 424,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Indicates that the server is unwilling to risk processing a request
        /// that might be replayed.
        /// </summary>
        TooEarly = 425,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The server refuses to perform the request using the current
        /// protocol but might be willing to do so after the client upgrades to
        /// a different protocol.  The server sends an Upgrade header in a 426
        /// response to indicate the required protocol(s).
        /// </summary>
        UpgradeRequired = 426,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The origin server requires the request to be conditional.  This
        /// response is intended to prevent the "lost update" problem, where a
        /// client GETs the state of a resource, modifies it and PUTs it back
        /// to the server, when meanwhile a third party has modified the state
        /// on the server, leading to a conflict.
        /// </summary>
        PreconditionRequired = 428,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The user has sent too many requests in a given amount of time
        /// ("rate limiting").
        /// </summary>
        TooManyRequests = 429,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The server is unwilling to process the request because its header
        /// fields are too large.  The request may be resubmitted after
        /// reducing the size of the request header fields.
        /// </summary>
        RequestHeaderFieldsTooLarge = 431,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The user agent requested a resource that cannot legally be
        /// provided, such as a web page censored by a government, et al.
        /// </summary>
        UnavailableForLegalReasons = 451,

        ///////////////////////////////////////////////////////////////////////
        // Server Error 5xx
        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The server has encountered a situation it does not know how to
        /// handle.
        /// </summary>
        InternalServerError = 500,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The request method is not supported by the server and cannot be
        /// handled.  The only methods that servers are required to support
        /// (and therefore that must not return this code) are GET and HEAD.
        /// </summary>
        NotImplemented = 501,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This error response means that the server, while working as a
        /// gateway to get a response needed to handle the request, got an
        /// invalid response.
        /// </summary>
        BadGateway = 502,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The server is not ready to handle the request. Common causes are a
        /// server that is down for maintenance or that is overloaded. Note
        /// that together with this response, a user-friendly page explaining
        /// the problem should be sent.  This response should be used for
        /// temporary conditions and the Retry-After HTTP header should, if
        /// possible, contain the estimated time before the recovery of the
        /// service.  The webmaster must also take care about the
        /// caching-related headers that are sent along with this response, as
        /// these temporary condition responses should usually not be cached.
        /// </summary>
        ServiceUnavailable = 503,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This error response is given when the server is acting as a gateway
        /// and cannot get a response in time.
        /// </summary>
        GatewayTimeout = 504,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The HTTP version used in the request is not supported by the
        /// server.
        /// </summary>
        HttpVersionNotSupported = 505,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The server has an internal configuration error: the chosen variant
        /// resource is configured to engage in transparent content negotiation
        /// itself, and is therefore not a proper end point in the negotiation
        /// process.
        /// </summary>
        VariantAlsoNegotiates = 506,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The method could not be performed on the resource because the
        /// server is unable to store the representation needed to successfully
        /// complete the request.
        /// </summary>
        InsufficientStorage = 507,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The server detected an infinite loop while processing the request.
        /// </summary>
        LoopDetected = 508,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Further extensions to the request are required for the server to
        /// fulfill it.
        /// </summary>
        NotExtended = 510,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Indicates that the client needs to authenticate to gain network
        /// access.
        /// </summary>
        NetworkAuthenticationRequired = 511,

        ///////////////////////////////////////////////////////////////////////
        // Unknown Error 9xx
        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The server failed in a way that is not known -OR- it does not wish
        /// to reveal a more specific root cause.
        /// </summary>
        RequestDenied = 999
    }

    ///////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// This enumeration represents the access control operations that can be
    /// performed, e.g. on the "safe" interpreter sandboxes, et al.
    /// </summary>
    [Flags()]
#if KAPOK
    [ObjectId("283eab12-895f-4b5c-b218-a925b61cc4dd")]
#else
    [Guid("283eab12-895f-4b5c-b218-a925b61cc4dd")]
#endif
    public enum AccessChangeType : ulong
    {
        /// <summary>
        /// This value is a placeholder that means "do nothing".
        /// </summary>
        None = 0x0,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This value means that access should be granted.
        /// </summary>
        Grant = 0x1000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This value means that access should be revoked.  This is the
        /// default state.
        /// </summary>
        Revoke = 0x2000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This value means that access has been banned.  Access will not be
        /// allowed, and other access checks will be skipped.
        /// </summary>
        Ban = 0x4000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This value means that access has been unbanned.  Access may be
        /// allowed, depending on the other access checks.  This is the
        /// default state.
        /// </summary>
        Unban = 0x8000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This value means that administrator access has been granted.
        /// </summary>
        Promote = 0x10000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This value means that administrator access has been revoked.  This
        /// is the default state.
        /// </summary>
        Demote = 0x20000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This value means that simulated-only access has been granted.
        /// </summary>
        Fake = 0x40000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This value means that both simulated-only and real access has been
        /// granted.  This is the default state.
        /// </summary>
        Real = 0x80000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This value the sandbox should be restricted to a particular subset
        /// of commands, i.e. starting from the subset of commands that are
        /// allowed based on the interpreter "safety" status and then further
        /// restricting those.  This option generally requires the "value"
        /// argument, which must represent the ruleset to use.
        /// </summary>
        Restrict = 0x100000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This value the sandbox should not be restricted to a particular
        /// subset of commands; instead, it will contain the full subset of
        /// commands that are allowed based on the interpreter "safety" status.
        /// This is the default state.
        /// </summary>
        Unrestrict = 0x200000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This value means that the request throttle subsystem is being
        /// queried for hits.
        /// </summary>
        Hits = 0x10000000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This value means that the access is being checked based on the
        /// request throttle subsystem.  This option generally requires the
        /// "value" argument, which must represent the candidate (client)
        /// host or IP address.
        /// </summary>
        Throttle = 0x20000000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This value means that the request throttle subsystem needs to be
        /// reset.
        /// </summary>
        Reset = 0x40000000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This means request throttling is based on sliding-windows instead
        /// of a fixed-window.
        /// </summary>
        Sliding = 0x80000000
    }

    ///////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// This enumeration is used when reading settings that are needed by the
    /// various endpoints exposed by the web server.
    /// </summary>
    [Flags()]
#if KAPOK
    [ObjectId("f9b6e195-1bc6-4809-be6a-96d966e0e6f2")]
#else
    [Guid("f9b6e195-1bc6-4809-be6a-96d966e0e6f2")]
#endif
    public enum SettingDataType : ulong
    {
        /// <summary>
        /// No special handling.
        /// </summary>
        None = 0x0,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Reserved, do not use.
        /// </summary>
        Reserved = 0x2,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The setting value should be True, False, 1, 0, et al.
        /// </summary>
        Boolean = 0x100,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The setting value should be a 32-bit integer.
        /// </summary>
        Integer = 0x200,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The setting value should be a 64-bit integer.
        /// </summary>
        WideInteger = 0x400,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The setting value should be an <see cref="Enum" /> value.
        /// </summary>
        Enumeration = 0x800,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The setting value should be a plain old string.
        /// </summary>
        String = 0x1000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The setting value should be an Eagle formatted list value.
        /// </summary>
        List = 0x2000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The setting value should be an Eagle script to be evaluated.
        /// </summary>
        Script = 0x4000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The setting value should be the name of a <see cref="Type" />.
        /// </summary>
        TypeName = 0x8000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The setting value should be the name of an <see cref="Encoding" />.
        /// </summary>
        EncodingName = 0x10000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The setting value should be the name of a file on the local file
        /// system.
        /// </summary>
        FileName = 0x20000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The setting value should be the name of a directory on the local
        /// file system.
        /// </summary>
        DirectoryName = 0x40000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The setting read operation is using (at least) the default flags,
        /// as defined by the <see cref="Default" /> value.
        /// </summary>
        ForDefault = 0x100000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The setting value is allowed to be null or an empty string.
        /// </summary>
        AllowEmpty = 0x1000000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The setting value is allowed to refer to a file or directory that
        /// does not exist on the local file system, e.g. for a log file to be
        /// created.
        /// </summary>
        NoExists = 0x2000000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The setting value may contain tokens to be replaced with their
        /// associated runtime values.
        /// </summary>
        ExpandTokens = 0x4000000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Do not consider any of the alternate setting names (i.e. with the
        /// "1" to "9" suffixes).
        /// </summary>
        NoSearch = 0x8000000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// If none of the candidate values can be verified, return null;
        /// otherwise, the final value will be returned, even if it is not
        /// valid.
        /// </summary>
        MustVerify = 0x10000000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Create the file or directory if it does not exist.
        /// </summary>
        CreatePath = 0x20000000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Skip querying the available script variables, if any.
        /// </summary>
        NoVariableValue = 0x40000000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Skip querying the available application settings, if any.
        /// </summary>
        NoAppSetting = 0x80000000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Skip querying the available environment variables, if any.
        /// </summary>
        NoEnvironment = 0x100000000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// (Even) if everything goes "perfectly", maybe emit a diagnostic
        /// message.
        /// </summary>
        TraceOk = 0x200000000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// If there are any failures, maybe emit a diagnostic message.
        /// </summary>
        TraceError = 0x400000000,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This is the typical set of flags used when querying a setting that
        /// should be an Eagle formatted list.
        /// </summary>
        StringListMask = Default | String | List | AllowEmpty,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This is the typical set of base flags used when querying a setting
        /// that should be a file or directory name.
        /// </summary>
        PathMask = FileName | DirectoryName,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This is the typical set of extra flags used when querying a setting
        /// that should be an existing directory name.  The directory should be
        /// created if it does not already exist.
        /// </summary>
        PathFlagsMask = NoExists | CreatePath,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// These are the possible flags that restrict the sources of returned
        /// setting values.
        /// </summary>
        ExcludeMask = NoVariableValue | NoAppSetting | NoEnvironment,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// These are the possible extra flags that may be used with various
        /// base flags.
        /// </summary>
        FlagsMask = AllowEmpty | NoExists | ExpandTokens |
                    NoSearch | MustVerify | CreatePath |
                    NoVariableValue | NoAppSetting | NoEnvironment,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This is the typical set of flags used when querying a setting that
        /// should use default semantics -AND- expand any dynamic tokens at
        /// runtime.
        /// </summary>
        DefaultAndExpand = Default | ExpandTokens,

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This is the typical set of flags used when querying a setting.
        /// </summary>
        Default = MustVerify | ForDefault
    }
}
