dotnet test -f netcoreapp2.1 ^
    /p:CollectCoverage=true ^
    /p:CoverletOutputFormat=opencover ^
    /p:CoverletOutputDirectory="%~dp0TestResults" ^
    test\InfoCarrier.Core.FunctionalTests\InfoCarrier.Core.FunctionalTests.csproj
