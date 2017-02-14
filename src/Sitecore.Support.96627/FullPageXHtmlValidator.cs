using Sitecore.Data.Items;
using Sitecore.Data.Validators;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Layouts;
using Sitecore.Links;
using Sitecore.Sites;
using Sitecore.Text;
using Sitecore.Web;
using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Web;
using System.Web.Configuration;

namespace Sitecore.Support.Data.Validators.ItemValidators
{
    [Serializable]
    public class FullPageXHtmlValidator : StandardValidator
    {
        public override string Name
        {
            get
            {
                return "Full Page is XHtml";
            }
        }

        public FullPageXHtmlValidator()
        {
        }

        public FullPageXHtmlValidator(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        protected override ValidatorResult Evaluate()
        {
            Item item = base.GetItem();
            if (item == null)
            {
                return ValidatorResult.Valid;
            }
            if (!item.Paths.IsContentItem)
            {
                return ValidatorResult.Valid;
            }
            if (item.Visualization.Layout == null)
            {
                return ValidatorResult.Valid;
            }
            UrlString url = this.GetUrl(item);
            string text = url.ToString();
            if (text.IndexOf("://", StringComparison.InvariantCulture) < 0)
            {
                text = WebUtil.GetServerUrl() + text;
            }
            HttpWebRequest httpWebRequest = FullPageXHtmlValidator.CreateRequest(text);
            string text2 = string.Empty;
            try
            {
                WebResponse response = httpWebRequest.GetResponse();
                Stream responseStream = response.GetResponseStream();
                if (responseStream != null)
                {
                    StreamReader streamReader = new StreamReader(responseStream);
                    text2 = streamReader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                ValidatorResult result;
                if (((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.NotFound)
                {
                    result = ValidatorResult.Valid;
                    return result;
                }
                base.Text = base.GetText(Translate.Text("The page represented by the item '{0}' failed to render properly, The error was: {1}", new object[]
                {
                    item.Paths.ContentPath,
                    ex.Message
                }), new string[0]);
                result = base.GetFailedResult(ValidatorResult.Error);
                return result;
            }
            text2 = XHtml.AddHtmlEntityConversionsInDoctype(text2);
            Collection<XHtmlValidatorError> collection = XHtml.Validate(text2);
            if (collection.Count == 0)
            {
                return ValidatorResult.Valid;
            }
            foreach (XHtmlValidatorError current in collection)
            {
                base.Errors.Add(string.Format("{0}: {1} [{2}, {3}]", new object[]
                {
                    current.Severity,
                    current.Message,
                    current.LineNumber,
                    current.LinePosition
                }));
            }
            base.Text = base.GetText(Translate.Text("The page represented by the item '{0}' contains (or lacks) some formatting attributes which could cause in unexpected results in some browsers (such as Internet Explorer, Firefox, or Safari)", new object[]
            {
                item.Paths.ContentPath
            }), new string[0]);
            return base.GetFailedResult(ValidatorResult.Error);
        }

        protected override ValidatorResult GetMaxValidatorResult()
        {
            return base.GetFailedResult(ValidatorResult.Error);
        }

        private UrlString GetUrl(Item item)
        {
            Assert.ArgumentNotNull(item, "item");
            UrlOptions defaultOptions = UrlOptions.DefaultOptions;
            defaultOptions.Site = SiteContext.GetSite("shell");
            string text = LinkManager.GetItemUrl(item, defaultOptions);
            if (text == "/sitecore/shell")
            {
                text += item.Name;
            }
            UrlString urlString = new UrlString(text);
            urlString.Add("sc_database", Client.ContentDatabase.Name);
            urlString.Add("sc_duration", "temporary");
            urlString.Add("sc_itemid", item.ID.ToString());
            urlString.Add("sc_lang", item.Language.Name);
            urlString.Add("sc_webedit", "0");
            if (base.Parameters.ContainsKey("device"))
            {
                urlString.Add("sc_device", base.Parameters["device"]);
            }
            return urlString;
        }

        private static HttpWebRequest CreateRequest(string url)
        {
            Assert.ArgumentNotNull(url, "url");
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
            httpWebRequest.UserAgent = HttpContext.Current.Request.UserAgent;
            SessionStateSection sessionStateSection = (SessionStateSection)ConfigurationManager.GetSection("system.web/sessionState");
            string cookieName = sessionStateSection.CookieName;
            CookieContainer cookieContainer = new CookieContainer();
            Uri uri = new Uri(url);
            HttpCookieCollection cookies = HttpContext.Current.Request.Cookies;
            for (int i = 0; i < cookies.Count; i++)
            {
                HttpCookie httpCookie = cookies[i];
                if (cookieName != httpCookie.Name)
                {
                    cookieContainer.Add(new Cookie(httpCookie.Name, httpCookie.Value, httpCookie.Path, uri.Host));
                }
            }
            httpWebRequest.CookieContainer = cookieContainer;
            return httpWebRequest;
        }
    }
}
