using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Web.Routing;
using MrCMS.Services;
using MrCMS.Shortcodes;
using MrCMS.Website;
using MrCMS.Website.Controllers;
using Newtonsoft.Json;
using Ninject;

namespace MrCMS.Helpers
{
    public static class MrCMSHtmlHelper
    {
        public static MvcHtmlString DeleteCheckBoxFor<TModel>(this HtmlHelper<TModel> htmlHelper,
                                                              Expression<Func<TModel, object>> expression)
        {
            return DeleteCheckBoxFor(htmlHelper, expression, new RouteValueDictionary());
        }

        public static MvcHtmlString DeleteCheckBoxFor<TModel>(this HtmlHelper<TModel> htmlHelper,
                                                              Expression<Func<TModel, object>> expression,
                                                              object htmlAttributes)
        {
            return DeleteCheckBoxFor(htmlHelper, expression, new RouteValueDictionary(htmlAttributes));
        }

        public static MvcHtmlString DeleteCheckBoxFor<TModel>(this HtmlHelper<TModel> htmlHelper,
                                                              Expression<Func<TModel, object>> expression,
                                                              IDictionary<string, object> htmlAttributes)
        {
            if (expression == null)
                throw new ArgumentNullException("expression");

            ModelMetadata metadata = ModelMetadata.FromLambdaExpression(expression, htmlHelper.ViewData);
            bool? isChecked = null;
            if (metadata.Model != null)
            {
                bool modelChecked;
                if (Boolean.TryParse(metadata.Model.ToString(), out modelChecked))
                    isChecked = modelChecked;
            }

            return CheckBoxHelper(htmlHelper, metadata,
                                  String.Format("{0}.Deleted", ExpressionHelper.GetExpressionText(expression)),
                                  isChecked, htmlAttributes);
        }

        private static MvcHtmlString CheckBoxHelper<TModel>(HtmlHelper<TModel> htmlHelper, ModelMetadata metadata,
                                                            string name, bool? isChecked,
                                                            IDictionary<string, object> htmlAttributes)
        {
            bool explicitValue = isChecked.HasValue;
            if (explicitValue)
                htmlAttributes.Remove("checked"); // Explicit value must override dictionary

            return InputHelper(htmlHelper, InputType.CheckBox, metadata, name, "true", !explicitValue /* useViewData */,
                               isChecked ?? false, true /* setId */, false /* isExplicitValue */, htmlAttributes);
        }

        private static MvcHtmlString InputHelper(HtmlHelper htmlHelper, InputType inputType, ModelMetadata metadata,
                                                 string name, object value, bool useViewData, bool isChecked, bool setId,
                                                 bool isExplicitValue, IDictionary<string, object> htmlAttributes)
        {
            string fullName = htmlHelper.ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(name);

            var tagBuilder = new TagBuilder("input");
            tagBuilder.MergeAttributes(htmlAttributes);
            tagBuilder.MergeAttribute("type", HtmlHelper.GetInputTypeString(inputType));
            tagBuilder.MergeAttribute("name", fullName, true);

            string valueParameter = Convert.ToString(value, CultureInfo.CurrentCulture);
            bool usedModelState = false;

            switch (inputType)
            {
                case InputType.CheckBox:
                    var modelStateWasChecked = htmlHelper.GetModelStateValue(fullName, typeof(bool)) as bool?;
                    if (modelStateWasChecked.HasValue)
                    {
                        isChecked = modelStateWasChecked.Value;
                        usedModelState = true;
                    }
                    goto case InputType.Radio;
                case InputType.Radio:
                    if (!usedModelState)
                    {
                        var modelStateValue = htmlHelper.GetModelStateValue(fullName, typeof(string)) as string;
                        if (modelStateValue != null)
                        {
                            isChecked = String.Equals(modelStateValue, valueParameter, StringComparison.Ordinal);
                            usedModelState = true;
                        }
                    }
                    if (!usedModelState && useViewData)
                        isChecked = htmlHelper.EvalBoolean(fullName);

                    if (isChecked)
                        tagBuilder.MergeAttribute("checked", "checked");

                    tagBuilder.MergeAttribute("value", valueParameter, isExplicitValue);
                    break;
                case InputType.Password:
                    if (value != null)
                        tagBuilder.MergeAttribute("value", valueParameter, isExplicitValue);
                    break;
                default:
                    var attemptedValue = (string)htmlHelper.GetModelStateValue(fullName, typeof(string));
                    tagBuilder.MergeAttribute("value",
                                              attemptedValue ??
                                              ((useViewData) ? htmlHelper.EvalString(fullName) : valueParameter),
                                              isExplicitValue);
                    break;
            }

            if (setId)
            {
                tagBuilder.GenerateId(fullName);
            }

            // If there are any errors for a named field, we add the css attribute.
            ModelState modelState;
            if (htmlHelper.ViewData.ModelState.TryGetValue(fullName, out modelState))
            {
                if (modelState.Errors.Count > 0)
                {
                    tagBuilder.AddCssClass(HtmlHelper.ValidationInputCssClassName);
                }
            }

            tagBuilder.MergeAttributes(htmlHelper.GetUnobtrusiveValidationAttributes(name, metadata));

            if (inputType == InputType.CheckBox)
            {
                // Render an additional <input type="hidden".../> for checkboxes. This 
                // addresses scenarios where unchecked checkboxes are not sent in the request.
                // Sending a hidden input makes it possible to know that the checkbox was present 
                // on the page when the request was submitted. 
                var inputItemBuilder = new StringBuilder();
                inputItemBuilder.Append(tagBuilder.ToString(TagRenderMode.SelfClosing));

                var hiddenInput = new TagBuilder("input");
                hiddenInput.MergeAttribute("type", HtmlHelper.GetInputTypeString(InputType.Hidden));
                hiddenInput.MergeAttribute("name", fullName);
                hiddenInput.MergeAttribute("value", "false");
                inputItemBuilder.Append(hiddenInput.ToString(TagRenderMode.SelfClosing));
                return MvcHtmlString.Create(inputItemBuilder.ToString());
            }

            return tagBuilder.ToMvcHtmlString(TagRenderMode.SelfClosing);
        }

        internal static MvcHtmlString LabelHelper(HtmlHelper html, ModelMetadata metadata, string htmlFieldName,
                                                  IDictionary<string, object> htmlAttributes, string labelText = null)
        {
            string resolvedLabelText = labelText ??
                                       metadata.DisplayName ?? metadata.PropertyName ?? htmlFieldName.Split('.').Last();
            if (String.IsNullOrEmpty(resolvedLabelText))
                return MvcHtmlString.Empty;

            var tag = new TagBuilder("label");
            tag.Attributes.Add("for",
                               TagBuilder.CreateSanitizedId(
                                   html.ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(htmlFieldName)));

            if (htmlAttributes != null)
                tag.MergeAttributes(htmlAttributes);

            tag.SetInnerText(resolvedLabelText);
            return tag.ToMvcHtmlString(TagRenderMode.Normal);
        }

        private static object GetModelStateValue(this HtmlHelper html, string key, Type destinationType)
        {
            ModelState modelState;
            if (html.ViewData.ModelState.TryGetValue(key, out modelState) && modelState.Value != null)
                return modelState.Value.ConvertTo(destinationType, null /* culture */);
            return null;
        }

        private static MvcHtmlString ToMvcHtmlString(this TagBuilder tagBuilder, TagRenderMode renderMode)
        {
            return new MvcHtmlString(tagBuilder.ToString(renderMode));
        }

        private static string EvalString(this HtmlHelper html, string key)
        {
            return Convert.ToString(html.ViewData.Eval(key), CultureInfo.CurrentCulture);
        }

        private static bool EvalBoolean(this HtmlHelper html, string key)
        {
            return Convert.ToBoolean(html.ViewData.Eval(key), CultureInfo.InvariantCulture);
        }

        public static MvcHtmlString FormLink(this HtmlHelper htmlHelper, string linkText, string actionName,
                                             string controllerName, object routeValues, object htmlAttributes)
        {
            var routeValuesDictionary = new RouteValueDictionary(routeValues);
            IDictionary<string, object> htmlAttributeDictionary = new RouteValueDictionary(htmlAttributes);
            htmlAttributeDictionary.Add("data-action", "post-link");
            return htmlHelper.ActionLink(linkText, actionName, controllerName, routeValuesDictionary,
                                         htmlAttributeDictionary);
        }

        public static MvcHtmlString AjaxFormLink(this HtmlHelper htmlHelper, string linkText, string actionName,
                                                 string controllerName, object routeValues, object htmlAttributes)
        {
            var routeValuesDictionary = new RouteValueDictionary(routeValues);
            IDictionary<string, object> htmlAttributeDictionary = new RouteValueDictionary(htmlAttributes);
            htmlAttributeDictionary.Add("data-action", "post-link-ajax");
            return htmlHelper.ActionLink(linkText, actionName, controllerName, routeValuesDictionary,
                                         htmlAttributeDictionary);
        }

        public static MvcHtmlString AjaxFormLink(this HtmlHelper htmlHelper, string linkText, string actionName,
                                                 object routeValues, object htmlAttributes)
        {
            return AjaxFormLink(htmlHelper, linkText, actionName, null, routeValues, htmlAttributes);
        }

        public static MvcHtmlString FormLink(this HtmlHelper htmlHelper, string linkText, string actionName,
                                             object routeValues, object htmlAttributes)
        {
            return FormLink(htmlHelper, linkText, actionName, null, routeValues, htmlAttributes);
        }

        public static MvcHtmlString LabelFor<TModel>(this HtmlHelper<TModel> htmlHelper,
                                                     Expression<Func<TModel, object>> expression, object htmlAttributes,
                                                     string text = null)
        {
            return LabelHelper(htmlHelper, ModelMetadata.FromLambdaExpression(expression, htmlHelper.ViewData),
                               ExpressionHelper.GetExpressionText(expression), new RouteValueDictionary(htmlAttributes),
                               text);
        }

        public static MvcHtmlString Label(this HtmlHelper htmlHelper, string labelFor, object htmlAttributes,
                                          string text = null)
        {
            return LabelHelper(htmlHelper, ModelMetadata.FromStringExpression(labelFor, htmlHelper.ViewData),
                               labelFor, new RouteValueDictionary(htmlAttributes), text);
        }

        public static IHtmlString ParseTweet(this string tweet)
        {
            var link =
                new Regex(@"http(s)?://([\w+?\.\w+])+([a-zA-Z0-9\~\!\@\#\$\%\^\&amp;\*\(\)_\-\=\+\\\/\?\.\:\;\'\,]*)?");
            var screenName = new Regex(@"@\w+");
            var hashTag = new Regex(@"#\w+");

            string formattedTweet = link.Replace(tweet, match =>
                                                            {
                                                                string val = match.Value;
                                                                return "<a href='" + val + "'>" + val + "</a>";
                                                            });

            formattedTweet = screenName.Replace(formattedTweet, match =>
                                                                    {
                                                                        string val = match.Value.Trim('@');
                                                                        return
                                                                            String.Format(
                                                                                "@<a href='http://twitter.com/{0}'>{1}</a>",
                                                                                val, val);
                                                                    });

            formattedTweet = hashTag.Replace(formattedTweet, match =>
                                                                 {
                                                                     string val = match.Value;
                                                                     return
                                                                         String.Format(
                                                                             "<a href='http://twitter.com/search/?q=%23{0}'>{1}</a>",
                                                                             val.Substring(1), val);
                                                                 });

            return new HtmlString(formattedTweet);
        }

        public static IHtmlString ParseShortcodes(this HtmlHelper htmlHelper, string content)
        {
            var shortcodeParsers = MrCMSApplication.GetAll<IShortcodeParser>();

            content = shortcodeParsers.Aggregate(content, (current, shortcodeParser) => shortcodeParser.Parse(htmlHelper, current));

            return new HtmlString(content);
        }

        public static IHtmlString RenderBodyContent(this HtmlHelper htmlHelper, string content)
        {
            var htmlContent = htmlHelper.ParseShortcodes(content);

            if (!MrCMSApplication.CurrentUserIsAdmin)
                return htmlContent;

            var sb = new StringBuilder();
            sb.Append("<div contenteditable='true' class='editable'>" + htmlContent + "</div>");

            return new HtmlString(sb.ToString());
        }

        public static MvcHtmlString RenderCustomAdminProperties<T>(this HtmlHelper<T> htmlHelper)
        {
            T model = htmlHelper.ViewData.Model;
            if (model == null)
                return MvcHtmlString.Empty;

            ViewEngineResult viewEngineResult =
                ViewEngines.Engines.FindView(
                    new ControllerContext(htmlHelper.ViewContext.RequestContext, new MrCMSController()),
                    model.GetType().Name, "");
            return viewEngineResult.View != null ? htmlHelper.Partial(model.GetType().Name, model) : MvcHtmlString.Empty;
        }

        public static MvcHtmlString RenderValue(this HtmlHelper htmlHelper, object value)
        {
            return value != null
                       ? MvcHtmlString.Create(IsJson(value.ToString())
                                                  ? GetPrettyPrintedJson(value.ToString())
                                                  : value.ToString())
                       : MvcHtmlString.Create(String.Empty);
        }

        private static string GetPrettyPrintedJson(string json)
        {
            dynamic parsedJson = JsonConvert.DeserializeObject(json);
            var prettyPrintedJson = string.Format("<pre>{0}</pre>",
                                                  JsonConvert.SerializeObject(parsedJson, Formatting.Indented));
            return prettyPrintedJson;
        }

        private static bool IsJson(string input)
        {
            input = input.Trim();
            return input.StartsWith("{") && input.EndsWith("}")
                   || input.StartsWith("[") && input.EndsWith("]");
        }

        public static MvcForm BeginForm(this HtmlHelper htmlHelper, FormMethod formMethod, object htmlAttributes)
        {
            // generates <form action="{current url}" method="post">...</form> 
            string formAction = htmlHelper.ViewContext.HttpContext.Request.RawUrl;
            return FormHelper(htmlHelper, formAction, formMethod, new RouteValueDictionary(htmlAttributes));
        }


        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope",
            Justification =
                "Because disposing the object would write to the response stream, you don't want to prematurely dispose of this object."
            )]
        private static MvcForm FormHelper(this HtmlHelper htmlHelper, string formAction, FormMethod method,
                                          IDictionary<string, object> htmlAttributes)
        {
            var tagBuilder = new TagBuilder("form");
            tagBuilder.MergeAttributes(htmlAttributes);
            // action is implicitly generated, so htmlAttributes take precedence.
            tagBuilder.MergeAttribute("action", formAction);
            // method is an explicit parameter, so it takes precedence over the htmlAttributes. 
            tagBuilder.MergeAttribute("method", HtmlHelper.GetFormMethodString(method), true);

            htmlHelper.ViewContext.Writer.Write(tagBuilder.ToString(TagRenderMode.StartTag));
            var theForm = new MvcForm(htmlHelper.ViewContext);

            return theForm;
        }
        public static string AbsoluteContent(this UrlHelper url, string path)
        {
            Uri uri = new Uri(path, UriKind.RelativeOrAbsolute);

            //If the URI is not already absolute, rebuild it based on the current request.
            if (!uri.IsAbsoluteUri)
            {
                Uri requestUrl = url.RequestContext.HttpContext.Request.Url;
                UriBuilder builder = new UriBuilder(requestUrl.Scheme, requestUrl.Host, requestUrl.Port)
                                         {
                                             Path =
                                                 VirtualPathUtility.ToAbsolute("~/" + path)
                                         };

                uri = builder.Uri;
            }

            return uri.ToString();
        }

        public static MvcHtmlString RenderImage(this HtmlHelper helper, string imageUrl, string alt = null, string title = null, object attributes = null)
        {
            if (String.IsNullOrWhiteSpace(imageUrl))
                return MvcHtmlString.Empty;

            var image = MrCMSApplication.Get<IImageProcessor>().GetImage(imageUrl);

            if (image == null)
                return MvcHtmlString.Empty;

            var tagBuilder = new TagBuilder("img");
            tagBuilder.Attributes.Add("src", imageUrl);
            tagBuilder.Attributes.Add("alt", alt ?? image.Title);
            tagBuilder.Attributes.Add("title", title ?? image.Description);
            if (attributes != null)
            {
                var routeValueDictionary = new RouteValueDictionary(attributes);
                foreach (var kvp in routeValueDictionary)
                {
                    tagBuilder.Attributes.Add(kvp.Key, kvp.Value.ToString());
                }
            }
            return MvcHtmlString.Create(tagBuilder.ToString(TagRenderMode.SelfClosing));
        }

        public static MvcHtmlString RenderImage(this HtmlHelper helper, string imageUrl, Size targetSize, string alt = null, string title = null, object attributes = null)
        {
            if (String.IsNullOrWhiteSpace(imageUrl))
                return MvcHtmlString.Empty;

            var imageProcessor = MrCMSApplication.Get<IImageProcessor>();
            var fileService = MrCMSApplication.Get<IFileService>();
            var image = imageProcessor.GetImage(imageUrl);

            if (image == null)
                return MvcHtmlString.Empty;

            if (ImageProcessor.RequiresResize(image.Size, targetSize))
            {
                var resized = ImageProcessor.CalculateDimensions(image.Size, targetSize);
                imageUrl = fileService.GetFileLocation(image, resized);
            }

            var tagBuilder = new TagBuilder("img");
            tagBuilder.Attributes.Add("src", imageUrl);
            tagBuilder.Attributes.Add("alt", alt ?? image.Title);
            tagBuilder.Attributes.Add("title", title ?? image.Description);
            if (attributes != null)
            {
                var routeValueDictionary = new RouteValueDictionary(attributes);
                foreach (var kvp in routeValueDictionary)
                {
                    tagBuilder.Attributes.Add(kvp.Key, kvp.Value.ToString());
                }
            }
            return MvcHtmlString.Create(tagBuilder.ToString(TagRenderMode.SelfClosing));
        }

        public static MvcHtmlString Link(this HtmlHelper helper, string text, string url, object htmlAttributes = null)
        {
            var tagBuilder = new TagBuilder("a");
            tagBuilder.Attributes.Add("href", ParseUrl(url));
            if (htmlAttributes != null)
            {
                var dictionary = new RouteValueDictionary(htmlAttributes);
                dictionary.ForEach(pair => tagBuilder.Attributes.Add(pair.Key, Convert.ToString(pair.Value)));
            }
            tagBuilder.InnerHtml = text;

            return MvcHtmlString.Create(tagBuilder.ToString());
        }

        private static string ParseUrl(string url)
        {
            Uri result;
            if (Uri.TryCreate(url, UriKind.Absolute, out result))
            {
                return result.ToString();
            }
            if (Uri.TryCreate("http://" + url, UriKind.Absolute, out result))
            {
                return result.ToString();
            }
            return url;
        }

        public static HtmlHelper GetHtmlHelper(Controller controller)
        {
            var viewContext = new ViewContext(controller.ControllerContext, new FakeView(), controller.ViewData, controller.TempData, TextWriter.Null);
            return new HtmlHelper(viewContext, new ViewPage());
        }

        private class FakeView : IView
        {
            public void Render(ViewContext viewContext, TextWriter writer)
            {
                throw new InvalidOperationException();
            }
        }

        public static RouteValueDictionary Merge(this RouteValueDictionary baseDictionary, RouteValueDictionary additions)
        {
            foreach (var key in additions)
                baseDictionary[key.Key] = key.Value;
            
            return baseDictionary;
        }
    }
}