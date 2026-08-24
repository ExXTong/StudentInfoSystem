# Course Schedule Frontend

学生信息服务前端，基于 React + Vite。

## 功能

- 登录（学生账号 / root 管理员）
- 课表查询
  - 学年 / 学期 / 课表类型 / 教学周 / 搜索
  - 表格形式展示
  - 今日课程
- 成绩查询
  - 学年 / 学期 / 所有学期
  - 成绩汇总
  - 成绩分布图
- 个人信息
- 管理后台（仅 root）
  - 用户管理
  - 访问统计
  - 登录历史
  - 公告管理
  - 系统参数配置
  - 本地数据备份 / 导入
- PWA
- 暗色模式
- 移动端适配
- 每次更新数据需重新输入密码（网页端）

## 开发

```bash
npm install
npm run dev
```

默认访问：

```text
http://localhost:5173
```

Vite 会将 `/api` 代理到：

```text
http://localhost:10000
```

## 构建

```bash
npm run build
npm run lint
```

输出在 `dist/`。

## 环境变量

```bash
VITE_API_BASE_URL=/api
```

可覆盖为：

```bash
VITE_API_BASE_URL=https://your-api.example.com/api
```
