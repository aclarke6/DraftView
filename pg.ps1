$env:PGPASSWORD = (dotnet user-secrets list --project DraftView.Web | Where-Object { $_ -match "PostgresPassword" } | ForEach-Object { $_ -replace "PostgresPassword = ", "" })
$pgArgs = @("-U", "postgres", "-d", "draftview") + $args
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" @pgArgs
