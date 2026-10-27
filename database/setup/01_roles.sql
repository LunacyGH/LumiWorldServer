-- Tạo ba role của LumiWorld. Chạy bằng postgres, chạy lại được (role có rồi thì chỉ đặt lại mật khẩu).
-- Mật khẩu truyền vào bằng biến psql, không ghi trong file:
--   scripts\setup.ps1 đọc .env rồi gọi psql -v owner_pw=... -v app_pw=... -v report_pw=...

-- lumi_owner: sở hữu bảng, chạy migration
select format('create role lumi_owner login password %L', :'owner_pw')
where not exists (select 1 from pg_roles where rolname = 'lumi_owner') \gexec
select format('alter role lumi_owner login password %L', :'owner_pw') \gexec

-- lumi_app: LumiServer dùng, chỉ đọc ghi dữ liệu
select format('create role lumi_app login password %L', :'app_pw')
where not exists (select 1 from pg_roles where rolname = 'lumi_app') \gexec
select format('alter role lumi_app login password %L', :'app_pw') \gexec

-- lumi_report: chỉ đọc view, cho dashboard
select format('create role lumi_report login password %L', :'report_pw')
where not exists (select 1 from pg_roles where rolname = 'lumi_report') \gexec
select format('alter role lumi_report login password %L', :'report_pw') \gexec
