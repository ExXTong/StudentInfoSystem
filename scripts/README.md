# NWUPL EAMS HTTP Replica

根据 `tam.nwupl.edu.cn.har` / `authserver.nwupl.edu.cn.har` 提取的纯 HTTP 复刻脚本，不依赖 Playwright。

## 功能

```bash
# 分析 HAR 中的关键请求流程
python3 scripts/nwupl_http_replica.py --analyze-har /var/nfs_share/tam.nwupl.edu.cn.har

# 离线解析 HAR 中的成绩/课表样例数据
python3 scripts/nwupl_http_replica.py --parse-har /var/nfs_share/tam.nwupl.edu.cn.har

# 尝试真实 HTTP 登录
python3 scripts/nwupl_http_replica.py --live \
  --username 学号 \
  --password '密码' \
  --proxy http://192.168.0.69:6152
```

## 已确认的请求

1. 登录
   - `POST https://authserver.nwupl.edu.cn/authserver/login?service=...`
   - 表单：`username`、`password`（AES 加密）、`captcha`、`_eventId=submit`、`cllt=userNameLogin`、`dllt=generalLogin`、`lt`、`execution`
2. 成绩
   - `GET /eams/teach/grade/course/person.action`
   - `GET /eams/teach/grade/course/person!search.action?semesterId=...&projectType=`
3. 课表
   - `GET /eams/courseTableForStd.action`
   - `POST /eams/courseTableForStd!courseTable.action`
4. 学籍
   - `GET /eams/stdDetail.action`

## 说明

- 密码加密：AES-128-CBC/PKCS7，随机前缀 + 随机 IV
- 登录页会动态下发 `pwdEncryptSalt`，脚本会自动提取
- 浏览器指纹 Cookie 可绕过验证码
- 当前环境需要走代理才能访问学校网站

## 本地一键启动

```bash
./scripts/start-local.sh
```
