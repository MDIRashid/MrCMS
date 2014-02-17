﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Iesi.Collections.Generic;
using Microsoft.AspNet.Identity;
using MrCMS.Entities.Documents.Layout;
using MrCMS.Entities.Documents.Media;
using MrCMS.Entities.Documents.Web;
using MrCMS.Entities.Multisite;
using MrCMS.Entities.People;
using MrCMS.Events;
using MrCMS.Helpers;
using MrCMS.Installation;
using MrCMS.Services;
using MrCMS.Settings;
using MrCMS.Web.Apps.Core.Pages;
using MrCMS.Web.Apps.Core.Widgets;
using MrCMS.Website;
using NHibernate;

namespace MrCMS.Web.Apps.Core
{
    public class CoreAppInstallation
    {
        public static void Install(ISession session, InstallModel model, Site site)
        {
//settings
            session.Transact(sess => sess.Save(site));
            CurrentRequestData.CurrentSite = site;

            var siteSettings = new SiteSettings
                                   {
                                       Site = site,
                                       TimeZone = model.TimeZone,
                                       UICulture = model.UiCulture
                                   };
            var mediaSettings = new MediaSettings
                                    {
                                        Site = site
                                    };
            var mailSettings = new MailSettings
                                   {
                                       Site = site
                                   };
            mailSettings.Port = 25;

            CurrentRequestData.SiteSettings = siteSettings;

            var documentService = new DocumentService(session,
                                                      new DocumentEventService(new List<IOnDocumentDeleted>(),
                                                                               new List<IOnDocumentUnpublished>(),
                                                                               new List<IOnDocumentAdded>()),
                                                      siteSettings, site);
            var layoutAreaService = new LayoutAreaService(session);
            var widgetService = new WidgetService(session);
            var fileSystem = new FileSystem();
            var imageProcessor = new ImageProcessor(session, fileSystem, mediaSettings);
            var fileService = new FileService(session, fileSystem, imageProcessor, mediaSettings, site, siteSettings);
            var user = new User
                           {
                               Email = model.AdminEmail,
                               IsActive = true
                           };

            var hashAlgorithms = new List<IHashAlgorithm> {new SHA512HashAlgorithm()};
            var hashAlgorithmProvider = new HashAlgorithmProvider(hashAlgorithms);
            var passwordEncryptionManager = new PasswordEncryptionManager(hashAlgorithmProvider,
                                                                          new UserService(session, siteSettings));
            var passwordManagementService = new PasswordManagementService(passwordEncryptionManager);

            passwordManagementService.ValidatePassword(model.AdminPassword, model.ConfirmPassword);
            passwordManagementService.SetPassword(user, model.AdminPassword, model.ConfirmPassword);
            var userService = new UserService(session, siteSettings);
            userService.AddUser(user);
            CurrentRequestData.CurrentUser = user;

            documentService.AddDocument(model.BaseLayout);
            var layoutAreas = new List<LayoutArea>
                                  {
                                      new LayoutArea
                                          {
                                              AreaName = "Main Navigation",
                                              CreatedOn = CurrentRequestData.Now,
                                              Layout = model.BaseLayout,
                                              Site = site
                                          },
                                      new LayoutArea
                                          {
                                              AreaName = "Header Left",
                                              CreatedOn = CurrentRequestData.Now,
                                              Layout = model.BaseLayout,
                                              Site = site
                                          },
                                      new LayoutArea
                                          {
                                              AreaName = "Header Right",
                                              CreatedOn = CurrentRequestData.Now,
                                              Layout = model.BaseLayout,
                                              Site = site
                                          },
                                      new LayoutArea
                                          {
                                              AreaName = "Before Content",
                                              CreatedOn = CurrentRequestData.Now,
                                              Layout = model.BaseLayout,
                                              Site = site
                                          },
                                      new LayoutArea
                                          {
                                              AreaName = "After Content",
                                              CreatedOn = CurrentRequestData.Now,
                                              Layout = model.BaseLayout,
                                              Site = site
                                          },
                                      new LayoutArea
                                          {
                                              AreaName = "Footer",
                                              CreatedOn = CurrentRequestData.Now,
                                              Layout = model.BaseLayout,
                                              Site = site
                                          }
                                  };

            foreach (LayoutArea l in layoutAreas)
                layoutAreaService.SaveArea(l);

            var navigationWidget = new Navigation();
            navigationWidget.LayoutArea = layoutAreas.Single(x => x.AreaName == "Main Navigation");
            widgetService.AddWidget(navigationWidget);


            widgetService.AddWidget(new UserLinks
                                        {
                                            Name = "User Links",
                                            LayoutArea = layoutAreas.Single(x => x.AreaName == "Header Right")
                                        });

            widgetService.AddWidget(new TextWidget
                                        {
                                            Name = "Footer text",
                                            Text = string.Format("<p>© Mr CMS {0}</p>",CurrentRequestData.Now.Year),
                                            LayoutArea = layoutAreas.Single(x => x.AreaName == "Footer")
                                        });

            documentService.AddDocument(model.HomePage);
            CurrentRequestData.HomePage = model.HomePage;
            documentService.AddDocument(model.Page2);
            documentService.AddDocument(model.Page3);
            documentService.AddDocument(model.Error403);
            documentService.AddDocument(model.Error404);
            documentService.AddDocument(model.Error500);

            var loginPage = new LoginPage
                                {
                                    Name = "Login",
                                    UrlSegment = "login",
                                    CreatedOn = CurrentRequestData.Now,
                                    Site = site,
                                    PublishOn = CurrentRequestData.Now,
                                    DisplayOrder = 100,
                                    RevealInNavigation = false
                                };
            documentService.AddDocument(loginPage);

            var forgottenPassword = new ForgottenPasswordPage
                                        {
                                            Name = "Forgot Password",
                                            UrlSegment = "forgot-password",
                                            CreatedOn = CurrentRequestData.Now,
                                            Site = site,
                                            PublishOn = CurrentRequestData.Now,
                                            Parent = loginPage,
                                            DisplayOrder = 0,
                                            RevealInNavigation = false
                                        };
            documentService.AddDocument(forgottenPassword);

            var resetPassword = new ResetPasswordPage
                                    {
                                        Name = "Reset Password",
                                        UrlSegment = "reset-password",
                                        CreatedOn = CurrentRequestData.Now,
                                        Site = site,
                                        PublishOn = CurrentRequestData.Now,
                                        Parent = loginPage,
                                        DisplayOrder = 1,
                                        RevealInNavigation = false
                                    };
            documentService.AddDocument(resetPassword);

            var userAccountPage = new UserAccountPage
                                      {
                                          Name = "My Account",
                                          UrlSegment = "my-account",
                                          CreatedOn = CurrentRequestData.Now,
                                          Site = site,
                                          PublishOn = CurrentRequestData.Now,
                                          Parent = loginPage,
                                          DisplayOrder = 1,
                                          RevealInNavigation = false
                                      };
            documentService.AddDocument(userAccountPage);

            var registerPage = new RegisterPage()
                                   {
                                       Name = "Register",
                                       UrlSegment = "register",
                                       CreatedOn = CurrentRequestData.Now,
                                       Site = site,
                                       PublishOn = CurrentRequestData.Now
                                   };
            documentService.AddDocument(registerPage);

            var webpages = session.QueryOver<Webpage>().List();
            webpages.ForEach(documentService.PublishNow);

            var defaultMediaCategory = new MediaCategory
                                           {
                                               Name = "Default",
                                               UrlSegment = "default",
                                               Site = site
                                           };
            documentService.AddDocument(defaultMediaCategory);


            siteSettings.DefaultLayoutId = model.BaseLayout.Id;
            siteSettings.Error403PageId = model.Error403.Id;
            siteSettings.Error404PageId = model.Error404.Id;
            siteSettings.Error500PageId = model.Error500.Id;
            siteSettings.EnableInlineEditing = true;
            siteSettings.SiteIsLive = true;

            mediaSettings.ThumbnailImageHeight = 50;
            mediaSettings.ThumbnailImageWidth = 50;
            mediaSettings.LargeImageHeight = 800;
            mediaSettings.LargeImageWidth = 800;
            mediaSettings.MediumImageHeight = 500;
            mediaSettings.MediumImageWidth = 500;
            mediaSettings.SmallImageHeight = 200;
            mediaSettings.SmallImageWidth = 200;
            mediaSettings.ResizeQuality = 90;
            mediaSettings.DefaultCategory = defaultMediaCategory.Id;

            var configurationProvider = new ConfigurationProvider(new SettingService(session, site),
                                                                  site);
            var fileSystemSettings = new FileSystemSettings {Site = site, StorageType = typeof (FileSystem).FullName};
            configurationProvider.SaveSettings(siteSettings);
            configurationProvider.SaveSettings(mediaSettings);
            configurationProvider.SaveSettings(fileSystemSettings);

            var logoPath = HttpContext.Current.Server.MapPath("/Apps/Core/Content/images/mrcms-logo.png");
            var fileStream = new FileStream(logoPath, FileMode.Open);
            var dbFile = fileService.AddFile(fileStream, Path.GetFileName(logoPath), "image/png", fileStream.Length,
                                             defaultMediaCategory);

            widgetService.AddWidget(new LinkedImage
                                        {
                                            Name = "Mr CMS Logo",
                                            Image = dbFile.url,
                                            Link = "/",
                                            LayoutArea = layoutAreas.Single(x => x.AreaName == "Header Left")
                                        });


            var adminUserRole = new UserRole
                                    {
                                        Name = UserRole.Administrator
                                    };

            user.Roles = new HashedSet<UserRole> {adminUserRole};
            adminUserRole.Users = new HashedSet<User> {user};
            var roleService = new RoleService(session);
            roleService.SaveRole(adminUserRole);

            var authorisationService = new AuthorisationService(HttpContext.Current.GetOwinContext().Authentication,
                                                                new UserManager<User>(new UserStore(userService,
                                                                                                    roleService, session)));
            authorisationService.Logout();
            authorisationService.SetAuthCookie(user, false);
        }
    }
}