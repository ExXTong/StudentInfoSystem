# NWUPL EAMS HTTP Replica

根据 `tam.nwupl.edu.cn.har` 提取的纯 HTTP 复刻实验脚本，不依赖 Playwright。

## 功能

```bash
# 分析 HAR 中的关键请求流程
python3 scripts/nwupl_http_replica.py --analyze-har /var/nfs_share/tam.nwupl.edu.cn.har

# 离线解析 HAR 中的成绩/课表样例数据
python3 scripts/nwupl_http_replica.py --parse-har /var/nfs_share/tam.nwupl.edu.cn.har

# 尝试真实 HTTP 登录（需要能访问 authserver.nwupl.edu.cn 的环境）
python3 scripts/nwupl_http_replica.py --live --username 学号 --password '密码'
```

## 已从 HAR 确认的请求

1. 登录
   - `POST https://authserver.nwupl.edu.cn/authserver/login?service=...`
   - 表单：`username`、`password`（加密）、`captcha`、`_eventId=submit`、`cllt=userNameLogin`、`dllt=generalLogin`、`lt`、`execution`

2. 成绩
   - `GET /eams/teach/grade/course/person.action`
   - `GET /eams/teach/grade/course/person!search.action?semesterId=...&projectType=`

3. 课表
   - `GET /eams/courseTableForStd.action`
   - `POST /eams/courseTableForStd!courseTable.action`
   - 参数：`ignoreHead=1&setting.kind=std&startWeek=&project.id=1&semester.id=194&ids=676535`

## 当前限制

- 当前环境无法访问 `authserver.nwupl.edu.cn` / `tam.nwupl.edu.cn`，所以不能在线执行完整登录。
- HAR 中没有登录页 HTML/JS，缺少密码加密算法（RSA 公钥/盐值）。要真正在线复刻登录，需要额外提供登录页 HTML 或该页面的加密 JS。
