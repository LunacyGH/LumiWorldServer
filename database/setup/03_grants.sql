-- Quyền trong một database. scripts\setup.ps1 chạy file này cho cả lumi và lumi_test.
-- Phải chạy trước migration: bảng và sequence do lumi_owner tạo sau đó tự có quyền cho lumi_app.

-- Chỉ lumi_owner được tạo bảng
revoke create on schema public from public;
grant usage on schema public to lumi_app, lumi_report;

-- Không gắn "in schema public": dotnet test xóa và tạo lại schema public, quyền mặc định vẫn còn
alter default privileges for role lumi_owner
    grant select, insert, update, delete on tables to lumi_app;
alter default privileges for role lumi_owner
    grant usage, select on sequences to lumi_app;

-- Bảng đã có từ trước (khi chạy lại file này sau migration)
grant select, insert, update, delete on all tables in schema public to lumi_app;
grant usage, select on all sequences in schema public to lumi_app;

-- lumi_report chỉ đọc view; view thêm ở giai đoạn 4 sẽ tự grant select to lumi_report
