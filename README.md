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

Kiem tra sau khi trien khai:

```powershell
python scripts/verify_api.py https://ngohoangduc-api.onrender.com
```
