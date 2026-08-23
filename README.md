# StudentInfoSystem

学生信息/成绩/课表查询系统，基于 .NET 8 微服务和 React + Vite 前端。

## 项目结构

| 项目 | 说明 | 默认端口 |
|---|---|---|
| `StudentInfoSystem.Gateway` | YARP 反向代理网关，统一 API 入口，JWT 认证 | 10000 / 10010 (HTTPS) |
| `StudentInfoSystem.AuthService` | 登录认证，签发 JWT | 10001 |
| `StudentInfoSystem.StudentService` | 学生信息爬取/查询 | 10002 |
| `StudentInfoSystem.GradeService` | 成绩查询 | 10003 |
| `StudentInfoSystem.ScheduleService` | 课表查询 | 10004 |
| `StudentInfoSystem.Common` | 公共模型、解析器、浏览器管理、安全中间件 | - |
| `course-schedule-frontend` | React + Vite 前端 | 5173 |

## 运行

### 后端

```bash
dotnet restore src.sln
dotnet run --project StudentInfoSystem.Gateway
```

然后按需启动 AuthService、StudentService、GradeService、ScheduleService。

### 前端

```bash
cd course-schedule-frontend
npm install
npm run dev
```

前端默认请求 `https://localhost:10010/api`，可通过环境变量覆盖：

```bash
VITE_API_BASE_URL=https://your-api.example.com/api npm run dev
```

## 配置说明

- JWT 签名密钥通过环境变量 `JWT__KEY` 或配置项 `Jwt:Key` 注入；生产环境不要使用仓库中的开发密钥。
- 若配置 `Security:GatewaySecret`，网关会向下游服务传递 `X-Gateway-Secret`，下游服务会校验该请求头，防止直接访问时伪造来源。
