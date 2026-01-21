version_suffix=preview.1.`date +%y%m%d%H%M`
dotnet build -c Debug --version-suffix $version_suffix
dotnet pack -c Debug --no-build --version-suffix $version_suffix -o ./oo 
dotnet tool uninstall -g NetCorePal.Extensions.CodeAnalysis.Tools
dotnet tool install -g NetCorePal.Extensions.CodeAnalysis.Tools --add-source ./oo --prerelease