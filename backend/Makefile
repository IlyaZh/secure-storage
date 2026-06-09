build:
	dotnet build ./src

update_db:
	dotnet ef database update --project ./src

watch:
	dotnet watch run --project ./src

test:
	dotnet test ./tests/SecureStorage.Tests/SecureStorage.Tests.csproj

db-connect:
	docker exec -it mysql_container mysql -uroot -ppass secret_share_db