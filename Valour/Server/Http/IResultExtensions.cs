﻿using System.Text.Json;
using Valour.Shared.Authorization;

namespace Valour.Server.Http
{
    public static class ValourResult
    {
        private struct OkResult : IResult
        {
            private readonly string _message;

            public OkResult(string message)
            {
                _message = message;
            }

            public async Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.StatusCode = 200;
                await httpContext.Response.WriteAsync(_message);
            }
        }
        
        private struct RawJsonResult : IResult
        {
            private readonly string _data;

            public RawJsonResult(string data)
            {
                _data = data;
            }

            public async Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.StatusCode = 200;
                httpContext.Response.Headers.ContentType = "application/json";
                await httpContext.Response.WriteAsync(_data);
            }
        }
        
        private struct JsonResult : IResult
        {
            private readonly object _data;

            public JsonResult(object data)
            {
                _data = data;
            }

            public async Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.StatusCode = 200;
                httpContext.Response.Headers.ContentType = "application/json";

                if (_data is null)
                {
                    await httpContext.Response.WriteAsync("null");
                }
                else
                {
                    await JsonSerializer.SerializeAsync(httpContext.Response.Body, _data);   
                }
            }
        }
        
        private readonly struct NullResult : IResult
        {
            public async Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.StatusCode = 200;
                await httpContext.Response.WriteAsync("null");
            }
        }

        private readonly struct NoTokenResult : IResult
        {
            public async Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.StatusCode = 401;
                await httpContext.Response.WriteAsync("Missing token.");
            }
        }

        private readonly struct ForbidResult : IResult
        {
            private readonly string _reason;

            public ForbidResult(string reason)
            {
                _reason = reason;
            }

            public async Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.StatusCode = 403;
                await httpContext.Response.WriteAsync(_reason);
            }
        }

        private readonly struct InvalidTokenResult : IResult
        {
            public async Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.StatusCode = 401;
                await httpContext.Response.WriteAsync("Token is invalid.");
            }
        }

        private readonly struct NotPlanetMemberResult : IResult
        {
            public async Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.StatusCode = 403;
                await httpContext.Response.WriteAsync("You are not a member of the target planet.");
            }
        }

        private readonly struct LacksPermissionResult : IResult
        {
            private readonly Permission _perm;

            public LacksPermissionResult(Permission perm)
            {
                _perm = perm;
            }

            public async Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.StatusCode = 403;
                await httpContext.Response.WriteAsync($"User lacks " + _perm.ReadableName + " Permission " + _perm.Name);
            }
        }

        private readonly struct NotFoundResult<T> : IResult
        {
            public async Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.StatusCode = 404;
                await httpContext.Response.WriteAsync($"{typeof(T).Name} was not found!");
            }
        }

        private readonly struct NotFoundResult : IResult
        {
            private readonly string _message;

            public NotFoundResult(string message)
            {
                _message = message;
            }

            public async Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.StatusCode = 404;
                await httpContext.Response.WriteAsync(_message);
            }
        }

        private readonly struct ProblemResult : IResult
        {
            private readonly string _message;

            public ProblemResult(string message)
            {
                _message = message;
            }

            public async Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.StatusCode = 500;
                await httpContext.Response.WriteAsync(_message);
            }
        }

        private readonly struct BadRequestResult : IResult
        {
            private readonly string _message;

            public BadRequestResult(string message)
            {
                _message = message;
            }

            public async Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.StatusCode = 400;
                await httpContext.Response.WriteAsync(_message);
            }
        }
        
        private readonly struct WrongNodeResult : IResult
        {
            private readonly string _node;
            private readonly long? _planetId;
            
            public WrongNodeResult(string node, long? planetId)
            {
                _node = node;
                _planetId = planetId;
            }
            
            public async Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.StatusCode = 421; // Misdirected
                await httpContext.Response.WriteAsync(_node); // Write the correct node name
                await httpContext.Response.WriteAsync(":");
                if (_planetId is not null)
                    await httpContext.Response.WriteAsync(_planetId.ToString()); // Write the planet ID
            }
        }

        public static IResult RawJson(string data) => new RawJsonResult(data);
        public static IResult Ok() => new OkResult("");
        public static IResult Ok(string message) => new OkResult(message);
        public static IResult BadRequest(string reason) => new BadRequestResult(reason);
        public static IResult Problem(string reason) => new ProblemResult(reason);
        public static IResult NotFound(string reason) => new NotFoundResult(reason);
        public static IResult NotFound<T>() => new NotFoundResult<T>();
        public static IResult Forbid(string reason) => new ForbidResult(reason);
        public static IResult NoToken() => _noToken;
        public static IResult InvalidToken() => _invalidToken;
        public static IResult NotPlanetMember() => _notPlanetMember;
        public static IResult LacksPermission(Permission permission) => new LacksPermissionResult(permission);
        public static IResult Json(object data) => new JsonResult(data);
        public static IResult WrongNode(string node, long? planetId) => new WrongNodeResult(node, planetId);
        public static IResult Null() => new NullResult();

        private static readonly IResult _noToken = new NoTokenResult();
        private static readonly IResult _invalidToken = new InvalidTokenResult();
        private static readonly IResult _notPlanetMember = new NotPlanetMemberResult();
    }
}
