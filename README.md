# StudentInfoSystem

学生课表 / 成绩 / 个人信息查询系统。

## 项目形态

| 端 | 技术 | 说明 |
|---|---|---|
| 网页端 | React + .NET 10 | 多用户 + 管理后台 + PWA |
| 桌面端 | Avalonia UI | 单用户本地 App |
| Android | .NET Android | 单用户本地 App |
| iOS | .NET iOS | 单用户本地 App |
| 核心库 | StudentInfoSystem.Core | 本地存储/会话逻辑 |
| 门户库 | StudentInfoSystem.Portal | 唯一 HTTP 客户端/解析器/模型（Web 与 App 共用） |

## 运行环境要求

- .NET SDK 10.0.400+
- Node.js / npm
- Android 端额外需要：
  - .NET Android workload
  - Android SDK

## 网页端快速启动

```bash
dotnet build src.sln
./scripts/start-local.sh
```

打开：

```text
http://localhost:5173
```

前端开发模式：

```bash
cd course-schedule-frontend
npm install
npm run dev
```

## 管理后台

- 管理员账号：`root`
- 默认密码：`root123456`（生产环境请修改）
- root 只显示管理功能
- 非管理员不显示管理入口

管理功能：

- 用户禁用 / 启用 / 重置
- 访问统计
- 登录历史
- 公告管理
- 系统参数配置
- 本地数据备份 / 导入

## 登录逻辑

- 首次登录：验证教务系统成功后保存凭据
- 后续登录：使用本地保存的凭据验证，不连接教务系统
- 更新数据：才连接教务系统获取最新数据
- 本地端密码使用系统安全存储（Windows DPAPI / Android Keystore / iOS Keychain）
- 网页端每次更新数据需重新输入密码

## PWA

网页端已支持 PWA：

- 可安装到桌面 / 手机
- 离线缓存
- 构建：

```bash
cd course-schedule-frontend
npm run build
```

## 本地 App

```text
apps/StudentInfoSystem.Desktop   Avalonia 桌面版
apps/StudentInfoSystem.Android   Android 版
apps/StudentInfoSystem.iOS       iOS 版
```

详细说明见：

```text
apps/README.md
```

## 核心库

```text
StudentInfoSystem.Core   本地存储/会话逻辑
StudentInfoSystem.Portal 唯一的 HTTP 客户端/解析器/模型
```

包含：

- 学生门户 HTTP 客户端
- CAS 登录 / AES 加密
- 成绩 / 课表 / 学生信息解析
- SQLite 本地存储
- 系统安全存储接口
