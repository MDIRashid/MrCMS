﻿using System.Collections.Generic;
using System.Web.Mvc;
using MrCMS.Entities.Documents.Media;
using MrCMS.Paging;
using MrCMS.Services;
using MrCMS.Settings;
using MrCMS.Website.Controllers;
using NHibernate;
using MrCMS.Helpers;
using NHibernate.Criterion;

namespace MrCMS.Web.Areas.Admin.Controllers
{
    public class MediaSelectorController : MrCMSAdminController
    {
        private readonly IMediaSelectorService _mediaSelectorService;

        public MediaSelectorController(IMediaSelectorService mediaSelectorService)
        {
            _mediaSelectorService = mediaSelectorService;
        }

        public ActionResult Show(MediaSelectorSearchQuery searchQuery)
        {
            ViewData["categories"] = _mediaSelectorService.GetCategories();
            ViewData["results"] = _mediaSelectorService.Search(searchQuery);
            return PartialView(searchQuery);
        }


        public JsonResult GetFileInfo(string value)
        {
            return Json(_mediaSelectorService.GetFileInfo(value), JsonRequestBehavior.AllowGet);
        }
    }

    public interface IMediaSelectorService
    {
        IPagedList<MediaFile> Search(MediaSelectorSearchQuery searchQuery);
        List<SelectListItem> GetCategories();
        SelectedItemInfo GetFileInfo(string value);
    }

    public class SelectedItemInfo
    {
        public string Url { get; set; }
    }

    public class MediaSelectorService : IMediaSelectorService
    {
        private readonly ISession _session;
        private readonly IFileService _fileService;

        public MediaSelectorService(ISession session, IFileService fileService)
        {
            _session = session;
            _fileService = fileService;
        }

        public IPagedList<MediaFile> Search(MediaSelectorSearchQuery searchQuery)
        {
            var queryOver = _session.QueryOver<MediaFile>();
            if (searchQuery.CategoryId.HasValue)
                queryOver = queryOver.Where(file => file.MediaCategory.Id == searchQuery.CategoryId);
            if (!string.IsNullOrWhiteSpace(searchQuery.Query))
            {
                queryOver =
                    queryOver.Where(
                        file =>
                        file.FileName.IsLike(searchQuery.Query, MatchMode.Anywhere) ||
                        file.Title.IsLike(searchQuery.Query, MatchMode.Anywhere) ||
                        file.Description.IsLike(searchQuery.Query, MatchMode.Anywhere));
            }
            return queryOver.OrderBy(file => file.CreatedOn).Desc.Paged(searchQuery.Page);
        }

        public List<SelectListItem> GetCategories()
        {
            return _session.QueryOver<MediaCategory>().Where(category => !category.HideInAdminNav).Cacheable().List()
                           .BuildSelectItemList(category => category.Name, category => category.Id.ToString(),
                                                emptyItemText: "All categories");
        }

        public SelectedItemInfo GetFileInfo(string value)
        {
            var fileUrl = _fileService.GetFileUrl(value);

            return new SelectedItemInfo
                       {
                           Url = fileUrl
                       };
        }
    }

    public class MediaSelectorSearchQuery
    {
        public MediaSelectorSearchQuery()
        {
            Page = 1;
        }
        public int Page { get; set; }
        public int? CategoryId { get; set; }

        public string Query { get; set; }
    }
}