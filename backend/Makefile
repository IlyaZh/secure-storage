build:
	dotnet build ./src

update_db:
	dotnet ef database update --project ./src

watch:
	dotnet watch run --project ./src

db-connect:
	docker exec -it mysql_container mysql -uroot -ppass secret_share_db