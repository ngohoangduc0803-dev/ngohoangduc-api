# Ngo Hoang Duc API

ASP.NET Core .NET 10 API cho nhan vien, ca lam va cham cong QR.

## Swagger UI

- Tren Render: https://ngohoangduc-api.onrender.com/docs
- Tren may: http://localhost:5143/docs
- OpenAPI JSON: `/openapi/v1.json`
- `/` va `/swagger` chuyen huong den `/docs`.

Trang tai lieu hoat dong trong ca Development va Production.

## Thu API

1. Mo `/docs`, thu `GET /health` bang nut Execute.
2. Tao tai khoan bang `POST /api/Auth/register`.
3. Goi `POST /api/Auth/login` de nhan OTP qua email.
4. Goi `POST /api/Auth/verify-otp` de lay `token`.
5. Bam **Authorize**, dan token (khong them `Bearer`), bam **Authorize**.
6. Thu `GET /api/Auth/me` va cac API phu hop voi quyen tai khoan.

API quan ly nhan vien va tao QR yeu cau Admin. Dang ky cong khai chi cap Employee.
Execute tren Swagger goi API that; cac lenh POST, PUT, DELETE co the thay doi du lieu.

## Chay tren may

Can .NET 10 SDK va cau hinh database/JWT/email hien co qua user secrets
hoac bien moi truong. Khong luu mat khau hay token vao Git.

```powershell
dotnet run --launch-profile http
```

## Kiem tra

```powershell
dotnet build --configuration Release
python scripts/verify_api.py http://localhost:5143
```

Script chi doc tai lieu, health va kiem tra tu choi token khong hop le;
khong tao tai khoan, gui email hay thay doi du lieu.

## Cap nhat Render

Dich vu hien co su dung `Dockerfile` (.NET 10, cong 10000).
Sau khi day code len nhanh ket noi voi Render, xem trang Events cua dich vu.
Neu Auto-Deploy dang tat, chon **Manual Deploy > Deploy latest commit**.
Giu nguyen cac bien moi truong database, JWT va email da cau hinh.

## Cau hinh email OTP tren Render

Backend gui OTP qua Resend HTTPS, khong dung SMTP. Dat trong Render > Environment:

- `Resend__ApiKey`: API key co quyen gui email, luu truc tiep tren Render.
- `Resend__FromEmail`: dia chi thuoc ten mien da xac minh tren Resend,
  vi du `noreply@your-domain.example` (thay bang ten mien cua ban).

`onboarding@resend.dev` chi gui thu den email cua chu tai khoan Resend.
De nhan vien dung email khac nhan OTP, can xac minh ten mien gui va doi FromEmail.
Khong dat FromEmail thanh dia chi Gmail neu khong so huu/xac minh duoc ten mien do.

API tra HTTP 503 kem ma loi de phan biet nguyen nhan:

| Ma | Can kiem tra |
| --- | --- |
| `EMAIL_NOT_CONFIGURED` | Thieu `Resend__ApiKey` |
| `EMAIL_TEST_MODE` | Dang dung dia chi gui thu `resend.dev`, nguoi nhan bi gioi han |
| `EMAIL_DOMAIN_NOT_VERIFIED` | Ten mien cua dia chi gui chua duoc xac minh |
| `EMAIL_PROVIDER_AUTH_FAILED` | API key/quyen gui email khong hop le |
| `EMAIL_RATE_LIMITED` | Dich vu email gioi han luot gui |
| `EMAIL_SERVICE_UNAVAILABLE` | Loi ket noi hoac dich vu email |

Log chi ghi ma loi, khong ghi OTP, API key hoac noi dung email.
Kiem tra phan loai loi bang HTTP gia lap (khong gui email that):

```powershell
dotnet run --project tests/EmailServiceChecks/EmailServiceChecks.csproj --configuration Release
```

Kiem tra sau khi trien khai:

```powershell
python scripts/verify_api.py https://ngohoangduc-api.onrender.com
```
