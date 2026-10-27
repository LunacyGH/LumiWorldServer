-- lumi: database thật. lumi_test: dotnet test xóa sạch và dựng lại mỗi lần chạy.
-- Chạy bằng postgres, chạy lại được.

select 'create database lumi owner lumi_owner encoding ''UTF8'' template template0'
where not exists (select 1 from pg_database where datname = 'lumi') \gexec

select 'create database lumi_test owner lumi_owner encoding ''UTF8'' template template0'
where not exists (select 1 from pg_database where datname = 'lumi_test') \gexec

-- Chỉ ba role của LumiWorld được kết nối
revoke connect, temporary on database lumi from public;
revoke connect, temporary on database lumi_test from public;
grant connect on database lumi to lumi_app, lumi_report;
grant connect on database lumi_test to lumi_app, lumi_report;
