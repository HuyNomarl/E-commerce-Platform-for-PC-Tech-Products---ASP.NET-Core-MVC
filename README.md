# Eshop

Eshop là một nền tảng thương mại điện tử xây dựng bằng ASP.NET Core MVC, tập trung vào bán lẻ linh kiện và thiết bị công nghệ, đồng thời mở rộng mạnh ở hai hướng: vận hành back-office và tích hợp AI/ML. Dự án không chỉ xử lý các bài toán storefront cơ bản như catalog, giỏ hàng và đặt hàng, mà còn bao gồm quản lý kho nhiều luồng, chat hỗ trợ realtime, thanh toán online, gợi ý sản phẩm cá nhân hóa và trợ lý AI tư vấn build PC.

## Tổng quan

Điểm nổi bật của dự án:

- Kiến trúc web app ASP.NET Core MVC hoàn chỉnh, tách rõ khu vực khách hàng và khu vực quản trị.
- Quản lý catalog, đơn hàng, vận chuyển, coupon, người dùng, vai trò, chat hỗ trợ và dashboard vận hành.
- Quản lý kho theo hướng nghiệp vụ thực tế: nhập kho, duyệt phiếu nhập, điều chỉnh, chuyển kho, reservation và rollback tồn.
- Recommendation cá nhân hóa bằng ML.NET.
- PC Builder tích hợp LLM + RAG + rule-based compatibility checking.
- Sử dụng SignalR, Hangfire, Redis Hybrid Cache, Cloudinary và SQL Server.

## Tính năng chính

### 1. Phía khách hàng

- Đăng ký, đăng nhập, quên mật khẩu, đăng nhập Google.
- Quản lý tài khoản, đổi mật khẩu, lịch sử đơn hàng, hủy đơn khi còn hợp lệ.
- Duyệt sản phẩm theo danh mục, thương hiệu, tìm kiếm và xem chi tiết sản phẩm.
- Giỏ hàng, checkout, COD, thanh toán VNPAY và MoMo.
- Wishlist và compare sản phẩm.
- Đánh giá sản phẩm.
- Chat hỗ trợ realtime với bộ phận support qua SignalR, hỗ trợ gửi sản phẩm, ảnh và video.
- PC Builder: chọn linh kiện, kiểm tra tương thích, lưu cấu hình, thêm vào giỏ, import/export Excel và chia sẻ cấu hình.

### 2. Phía quản trị

- Dashboard theo dõi doanh thu, đơn hàng, top sản phẩm, top khách hàng, tồn kho và reservation.
- Quản lý sản phẩm, danh mục, thương hiệu/publisher, slider, review, contact, shipping và coupon.
- Quản lý người dùng và vai trò.
- Phân quyền back-office theo nhiều vai trò: `Admin`, `CatalogManager`, `Publisher`, `OrderStaff`, `WarehouseManager`, `SupportStaff`.
- Quản lý kho gồm:
  - tạo và duyệt phiếu nhập kho,
  - điều chỉnh tồn,
  - chuyển kho,
  - xem lịch sử giao dịch kho,
  - theo dõi tồn theo sản phẩm,
  - theo dõi reservation.
- Quản lý chat hỗ trợ khách hàng.
- Kích hoạt train mô hình recommendation trong admin.
- Theo dõi job nền tại Hangfire Dashboard.

### 3. AI/ML và các mô-đun thông minh

- **Recommendation cá nhân hóa**:
  - Huấn luyện bằng `ML.NET MatrixFactorization`.
  - Dữ liệu huấn luyện lấy từ lịch sử mua hàng và wishlist.
  - Nếu thiếu model hoặc thiếu dữ liệu, hệ thống fallback sang sản phẩm bán chạy.

- **Trợ lý AI tư vấn build PC**:
  - Nhận yêu cầu bằng ngôn ngữ tự nhiên.
  - Trích xuất nhu cầu sử dụng, ngân sách, độ phân giải, game, ưu tiên linh kiện.
  - Kết hợp `LLM + RAG + catalog DB + compatibility rules`.
  - Trả về câu trả lời tiếng Việt, trạng thái hợp lệ, tổng giá, công suất ước tính và card sản phẩm gợi ý.

- **Catalog sync sang RAG**:
  - Dữ liệu sản phẩm được đồng bộ sang `rag-service`.
  - Hỗ trợ sync lúc startup, sync theo lịch và sync tăng dần khi catalog/tồn kho thay đổi.

## Kiến trúc và công nghệ

| Thành phần | Công nghệ |
| --- | --- |
| Backend web | ASP.NET Core MVC (`net10.0`) |
| ORM / Database | Entity Framework Core + SQL Server |
| Authentication / Authorization | ASP.NET Core Identity + Role/Policy |
| Cache / Session | Redis + Memory Cache + Hybrid Cache |
| Realtime | SignalR |
| Background jobs | Hangfire + SQL Server storage |
| AI/ML | ML.NET, OpenAI API, RAG service |
| Media storage | Cloudinary |
| Logging | Serilog |
| Thanh toán | VNPAY, MoMo |

## Cấu trúc thư mục chính

```text
Eshop.slnx
README.md
docs/                         Tài liệu phân tích nghiệp vụ, AI/ML, RAG, flowchart
Eshop/
  Areas/Admin/                Back-office controllers, views, view models
  Controllers/                Controllers phía storefront
  Services/                   Business services, AI services, inventory, payment
  Repository/                 DataContext, seeders, helpers cho dữ liệu
  Models/                     Domain models, enums, configuration classes
  Hubs/                       SignalR hubs
  Jobs/                       Hangfire jobs
  Views/                      Razor views storefront
  wwwroot/                    Static assets
  MLModels/                   Model recommendation và training data
  Migrations/                 EF Core migrations
```

## Yêu cầu môi trường

Để chạy đầy đủ dự án, bạn nên có:

- .NET SDK 10
- SQL Server
- Redis
- Tài khoản Cloudinary nếu dùng upload media/chat attachment
- SMTP nếu dùng email reset password / email workflow
- OpenAI API key nếu dùng AI chat
- `rag-service` nếu muốn bật đầy đủ PC Builder AI + catalog retrieval
- Thông tin sandbox/credential cho VNPAY hoặc MoMo nếu muốn kiểm tra thanh toán online

## Cấu hình

### 1. Cấu hình local

Repo đã loại bỏ secret hardcode khỏi `appsettings.json`. Cách gọn nhất là:

1. Sao chép file mẫu:

```powershell
Copy-Item .\Eshop\appsettings.Development.local.example.json .\Eshop\appsettings.Development.local.json
```

2. Điền các giá trị cần thiết trong file local vừa tạo.

Bạn cũng có thể dùng `dotnet user-secrets` thay cho file local:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=YOUR_SERVER;Database=EShopDb;Trusted_Connection=True;MultipleActiveResultSets=true;Integrated Security=True;TrustServerCertificate=True" --project .\Eshop\Eshop.csproj
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379" --project .\Eshop\Eshop.csproj
dotnet user-secrets set "OpenAI:ApiKey" "your-openai-api-key" --project .\Eshop\Eshop.csproj
```

### 2. Các nhóm cấu hình quan trọng

| Nhóm cấu hình | Mục đích |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | Kết nối SQL Server |
| `ConnectionStrings:Redis` | Redis cho cache/session |
| `GoogleKeys` | Đăng nhập Google |
| `CloudinarySettings` | Upload ảnh/video |
| `Smtp` | Email reset password / thông báo |
| `OpenAI` | LLM cho AI chat |
| `RagService` | Địa chỉ `rag-service` và namespace catalog |
| `MomoAPI` | Thanh toán MoMo |
| `Vnpay` | Thanh toán VNPAY |
| `Hangfire` | Dashboard path, lịch cleanup reservation, lịch sync catalog |

## Khởi động dự án

### 1. Restore package

```powershell
dotnet restore .\Eshop.slnx
```

### 2. Apply database migrations

```powershell
dotnet ef database update --project .\Eshop\Eshop.csproj --startup-project .\Eshop\Eshop.csproj
```

Nếu máy chưa có `dotnet-ef`, cài bằng:

```powershell
dotnet tool install --global dotnet-ef
```

### 3. Khởi động Redis

Đảm bảo Redis đang chạy ở địa chỉ khớp với `ConnectionStrings:Redis`, mặc định là:

```text
localhost:6379
```

### 4. Khởi động `rag-service` nếu dùng AI cho PC Builder

Mặc định app kỳ vọng RAG service ở:

```text
http://localhost:8001
```

Nếu chưa có `rag-service`, bạn nên:

- tắt `RagService:StartupFullSyncEnabled`, hoặc
- giữ nguyên nhưng chấp nhận các tính năng RAG/AI liên quan sẽ không hoạt động đầy đủ.

### 5. Chạy ứng dụng

```powershell
dotnet run --project .\Eshop\Eshop.csproj
```

Theo `launchSettings.json`, ứng dụng local mặc định chạy tại:

- `https://localhost:7185`
- `http://localhost:5254`

## Truy cập và bootstrap tài khoản quản trị

Hệ thống sẽ tự seed các role hệ thống khi ứng dụng khởi động lần đầu, nhưng **không tạo sẵn tài khoản admin mặc định**.

Điều này có nghĩa là lần bootstrap đầu tiên bạn cần:

1. Tạo một tài khoản người dùng bình thường.
2. Gán thủ công role `Admin` cho tài khoản đó trong database hoặc bằng quy trình bootstrap nội bộ của bạn.
3. Đăng nhập lại và truy cập `/Admin`.

Lưu ý bảo mật:

- Các tài khoản back-office bắt buộc phải bật 2FA trước khi vào khu vực quản trị.
- Hangfire Dashboard nằm tại `/Admin/Jobs` và chỉ cho phép `Admin`.

## Một số luồng kỹ thuật đáng chú ý

- **Reservation tồn kho khi checkout online**: hệ thống giữ chỗ tồn kho trước khi redirect sang cổng thanh toán, sau đó commit hoặc rollback theo callback thực tế.
- **Recommendation model**: lưu tại `Eshop/MLModels/recommendation-model.zip`, dữ liệu train được xuất thêm ra `training-data.csv`.
- **Support chat**: dùng SignalR để đẩy tin nhắn realtime giữa khách hàng và support/admin.
- **Catalog sync sang RAG**: job nền chạy qua Hangfire, hỗ trợ full-sync và incremental sync.

## Tài liệu liên quan

Nếu bạn muốn đi sâu hơn vào kiến trúc và phần AI/ML, xem thêm:

- [Chức năng AI/ML trong dự án](docs/chuc-nang-ai-ml-trong-du-an.md)
- [PC Builder RAG Integration](docs/pc-builder-rag-integration.md)
- [Phân tích chi tiết hai module AI/ML](docs/phan-tich-chi-tiet-hai-module-ai-ml.md)
- [Luận văn / phân tích tổng thể](docs/luan-van-eshop-ai-rag.md)

## Ghi chú triển khai

- `appsettings.json` chỉ nên chứa cấu hình mặc định, không lưu secret thật.
- Môi trường production nên dùng environment variables hoặc secret manager.
- Nếu không dùng một số tích hợp ngoài như Google, Cloudinary, OpenAI, MoMo hoặc VNPAY, bạn có thể để trống các khóa liên quan, nhưng các chức năng tương ứng sẽ bị vô hiệu hoặc hoạt động không đầy đủ.

## Tình trạng dự án

Đây là một codebase có phạm vi khá lớn, phù hợp cho đồ án hoặc hệ thống demo có chiều sâu kỹ thuật. Trọng tâm nổi bật của repo nằm ở:

- nghiệp vụ e-commerce end-to-end,
- quản trị vận hành theo role,
- quản lý kho có reservation,
- AI/ML và PC Builder thông minh.
