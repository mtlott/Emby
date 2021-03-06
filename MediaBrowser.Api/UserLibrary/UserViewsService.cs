﻿using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Library;
using MediaBrowser.Model.Querying;
using ServiceStack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Api.UserLibrary
{
    [Route("/Users/{UserId}/Views", "GET")]
    public class GetUserViews : IReturn<QueryResult<BaseItemDto>>
    {
        /// <summary>
        /// Gets or sets the user id.
        /// </summary>
        /// <value>The user id.</value>
        [ApiMember(Name = "UserId", Description = "User Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "GET")]
        public string UserId { get; set; }

        [ApiMember(Name = "IncludeExternalContent", Description = "Whether or not to include external views such as channels or live tv", IsRequired = true, DataType = "boolean", ParameterType = "query", Verb = "POST")]
        public bool? IncludeExternalContent { get; set; }
    }

    [Route("/Users/{UserId}/SpecialViewOptions", "GET")]
    public class GetSpecialViewOptions : IReturn<List<SpecialViewOption>>
    {
        /// <summary>
        /// Gets or sets the user id.
        /// </summary>
        /// <value>The user id.</value>
        [ApiMember(Name = "UserId", Description = "User Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "GET")]
        public string UserId { get; set; }
    }

    public class UserViewsService : BaseApiService
    {
        private readonly IUserManager _userManager;
        private readonly IUserViewManager _userViewManager;
        private readonly IDtoService _dtoService;

        public UserViewsService(IUserManager userManager, IUserViewManager userViewManager, IDtoService dtoService)
        {
            _userManager = userManager;
            _userViewManager = userViewManager;
            _dtoService = dtoService;
        }

        public async Task<object> Get(GetUserViews request)
        {
            var query = new UserViewQuery
            {
                UserId = request.UserId
            };

            if (request.IncludeExternalContent.HasValue)
            {
                query.IncludeExternalContent = request.IncludeExternalContent.Value;
            }

            var folders = await _userViewManager.GetUserViews(query, CancellationToken.None).ConfigureAwait(false);

            var dtoOptions = GetDtoOptions(request);

            var user = _userManager.GetUserById(request.UserId);

            var dtos = folders.Select(i => _dtoService.GetBaseItemDto(i, dtoOptions, user))
                .ToArray();

            var result = new QueryResult<BaseItemDto>
            {
                Items = dtos,
                TotalRecordCount = dtos.Length
            };

            return ToOptimizedResult(result);
        }

        public async Task<object> Get(GetSpecialViewOptions request)
        {
            var user = _userManager.GetUserById(request.UserId);

            var views = user.RootFolder
                .GetChildren(user, true)
                .OfType<ICollectionFolder>()
                .Where(IsEligibleForSpecialView)
                .ToList();

            var list = views
                .Select(i => new SpecialViewOption
                {
                    Name = i.Name,
                    Id = i.Id.ToString("N")

                })
            .OrderBy(i => i.Name)
            .ToList();

            return ToOptimizedResult(list);
        }

        private bool IsEligibleForSpecialView(ICollectionFolder view)
        {
            var types = new[] { CollectionType.Movies, CollectionType.TvShows, CollectionType.Games, CollectionType.Music, CollectionType.Photos };

            return types.Contains(view.CollectionType ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        }
    }

    class SpecialViewOption
    {
        public string Name { get; set; }
        public string Id { get; set; }
    }
}
