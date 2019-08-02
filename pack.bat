for /f "delims=" %%i in ('dotnet-gitversion /showvariable NuGetVersion') do set NuGetVersion=%%i

clean ^
  && dotnet build ^
  && dotnet test test\InfoCarrier.Core.FunctionalTests\InfoCarrier.Core.FunctionalTests.csproj ^
  && dotnet pack src\InfoCarrier.Core\InfoCarrier.Core.csproj --output "..\..\artifacts" --configuration Debug --include-symbols
