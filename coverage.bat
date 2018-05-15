dotnet test -f netcoreapp2.1 ^
    /p:CollectCoverage=true ^
    /p:CoverletOutputFormat=opencover ^
    /p:CoverletOutputDirectory="%~dp0TestResults" ^
    test\InfoCarrier.Core.FunctionalTests\InfoCarrier.Core.FunctionalTests.csproj

dotnet tools\ReportGenerator\ReportGenerator.dll -reports:TestResults\coverage.xml -targetdir:TestResults\coverage

start "" "TestResults\coverage\index.htm"
