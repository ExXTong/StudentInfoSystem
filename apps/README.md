# 多端 App

三端本地 App 共用 `StudentInfoSystem.Core` 和 `StudentInfoSystem.Portal`，登录/更新会话逻辑统一由 `PortalSession` 提供。

## 通用登录逻辑

- 首次登录：验证教务系统成功后保存凭据
- 后续登录：只校验本地凭据，不连接教务系统
- 更新数据：才连接教务系统
- 密码使用系统安全存储：
  - 桌面端：Windows DPAPI，其他平台 AES 加密文件
  - Android：Android Keystore（AES-GCM）
  - iOS：Keychain

## Avalonia 桌面版

```bash
cd StudentInfoSystem.Desktop
dotnet run
```

支持 Windows / macOS / Linux。

功能：登录、成绩、课表、学生信息、本地 SQLite 缓存、系统安全存储、设置代理/Cookie、导出成绩、清除缓存。

## Android 版

```bash
cd StudentInfoSystem.Android
dotnet restore
dotnet build -c Release -f net10.0-android
```

需要安装：

```bash
dotnet workload install android
```

以及 Android SDK。

功能：登录、成绩、课表、学生信息、本地 SQLite 缓存、Android Keystore、设置、导出、清除缓存。

## iOS 版

```bash
cd StudentInfoSystem.iOS
dotnet restore
dotnet build -f net10.0-ios
```

需要在 macOS 上执行，并安装 Xcode 和 .NET iOS workload。

功能：登录、成绩、课表、学生信息、本地 SQLite 缓存、Keychain、设置、导出、清除缓存。

## 目录结构

```text
apps/
  StudentInfoSystem.Desktop/
  StudentInfoSystem.Android/
  StudentInfoSystem.iOS/
```

共享逻辑在：

```text
StudentInfoSystem.Core/
```
