using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ngohoangduc.Documentation;

public static class ApiDocumentation
{
    public static void Configure(OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Info = new OpenApiInfo
            {
                Title = "Ngo Hoang Duc - API cham cong",
                Version = "v1",
                Description = "API quan ly nhan vien, ca lam va cham cong QR. "
                    + "Dang ky tai /api/Auth/register, dang nhap tai /api/Auth/login, "
                    + "xac minh OTP tai /api/Auth/verify-otp de nhan token. "
                    + "Bam Authorize va dan token de thu cac API co bieu tuong khoa. "
                    + "API quan ly nhan vien va tao QR yeu cau quyen Admin."
            };

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??=
                new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Dan token tra ve tu /api/Auth/verify-otp (khong them Bearer)."
            };

            return Task.CompletedTask;
        });

        options.AddOperationTransformer((operation, context, cancellationToken) =>
        {
            var metadata = context.Description.ActionDescriptor.EndpointMetadata;
            var requiresAuthorization = metadata.OfType<IAuthorizeData>().Any()
                && !metadata.OfType<IAllowAnonymous>().Any();

            if (requiresAuthorization)
            {
                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
                });

                operation.Responses ??= new OpenApiResponses();
                operation.Responses.TryAdd("401", new OpenApiResponse
                {
                    Description = "Chua dang nhap hoac token khong hop le."
                });
                operation.Responses.TryAdd("403", new OpenApiResponse
                {
                    Description = "Tai khoan khong co quyen thuc hien chuc nang nay."
                });
            }

            return Task.CompletedTask;
        });
    }
}
