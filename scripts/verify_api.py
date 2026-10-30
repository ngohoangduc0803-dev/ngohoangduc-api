"""Read-only checks for the deployed API documentation and authentication."""

import json
import sys
from urllib.error import HTTPError
from urllib.request import Request, urlopen


base_url = (sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:5143").rstrip("/")


def get(path, headers=None):
    request = Request(base_url + path, headers=headers or {})
    try:
        with urlopen(request, timeout=60) as response:
            return response.status, response.read()
    except HTTPError as error:
        return error.code, error.read()


status, body = get("/health")
assert status == 200 and json.loads(body)["status"] == "ok", "Health check failed"

status, body = get("/docs/index.html")
assert status == 200 and b"swagger-ui" in body, "Swagger UI is missing"
status, body = get("/docs/swagger-ui-bundle.js")
assert status == 200 and len(body) > 1000, "Swagger UI JavaScript is missing"

status, body = get("/openapi/v1.json")
assert status == 200, "OpenAPI document is missing"
document = json.loads(body)
scheme = document["components"]["securitySchemes"]["Bearer"]
assert scheme["type"] == "http" and scheme["scheme"] == "bearer"
paths = document["paths"]
assert not paths["/health"]["get"].get("security"), "Health must be public"
for action in ("register", "login", "verify-otp"):
    operation = paths[f"/api/Auth/{action}"]["post"]
    assert not operation.get("security"), f"{action} must be public"

protected_operations = 0
for path, methods in paths.items():
    if path.startswith(("/api/employees", "/api/workshifts", "/api/attendance")) or path == "/api/Auth/me":
        for method, operation in methods.items():
            if method not in ("get", "post", "put", "delete", "patch"):
                continue
            assert operation.get("security") == [{"Bearer": []}], f"Missing JWT requirement: {method} {path}"
            assert "401" in operation["responses"] and "403" in operation["responses"]
            protected_operations += 1

assert protected_operations > 0, "No protected endpoints documented"
assert "EmployeeInput" in document["components"]["schemas"]

for path in ("/api/employees", "/api/Auth/me", "/api/workshifts", "/api/attendance/history"):
    status, body = get(path)
    assert status == 401, f"Expected 401 without token at {path}, got {status}"
    status, body = get(path, {"Authorization": "Bearer invalid-token"})
    assert status == 401, f"Expected 401 with invalid token at {path}, got {status}"

for path in ("/", "/docs", "/swagger"):
    status, body = get(path)
    assert status == 200 and b"swagger-ui" in body, f"Documentation redirect failed: {path}"

print(f"PASS: Swagger UI, OpenAPI schemas, {protected_operations} protected operations, JWT rejection, redirects ({base_url})")
