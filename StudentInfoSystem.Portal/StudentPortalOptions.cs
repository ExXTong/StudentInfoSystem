using System;
using System.Collections.Generic;

namespace StudentInfoSystem.Portal
{
    /// <summary>
    /// 学生门户 HTTP 客户端配置。
    /// </summary>
    public class StudentPortalOptions
    {
        public string AuthServerBaseUrl { get; set; } = "https://authserver.nwupl.edu.cn";
        public string EamsBaseUrl { get; set; } = "https://tam.nwupl.edu.cn";
        public string ServiceUrl { get; set; } = "https://tam.nwupl.edu.cn/eams/homeExt.action";
        public string Proxy { get; set; } = "";
        public int TimeoutSeconds { get; set; } = 60;
        public string FingerprintCookies { get; set; } = "";
        public string UserAgent { get; set; } =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36 Edg/151.0.0.0";
    }
}
