FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build-env
WORKDIR /app

COPY PubSubFunction/PubSubFunction.csproj PubSubFunction/
COPY Common/Common.csproj Common/
RUN dotnet restore PubSubFunction/PubSubFunction.csproj

COPY . ./
RUN dotnet publish PubSubFunction -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:3.1
WORKDIR /app
EXPOSE 80
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "PubSubFunction.dll"]