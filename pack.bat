for /f "delims=" %%i in ('tools\GitVersion.CommandLine\tools\GitVersion.exe /showvariable NuGetVersion') do set NuGetVersion=%%i

clean ^
  && dotnet restore ^
  && dotnet test test\InfoCarrier.Core.FunctionalTests\InfoCarrier.Core.FunctionalTests.csproj ^
  && dotnet pack src\InfoCarrier.Core\InfoCarrier.Core.csproj --output "..\..\artifacts" --configuration Debug --include-symbols