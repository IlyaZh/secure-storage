db-connect:
	docker exec -it mysql_container mysql -uroot -ppass secret_share_db

up:
	docker compose up -d

down:
	docker compose down

build:
	docker compose up --build -d

logs:
	docker compose logs -f
