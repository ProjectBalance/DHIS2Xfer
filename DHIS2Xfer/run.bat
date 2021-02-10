%~d0
cd %~p0
start dotnet run
timeout 5
start http://localhost:5000