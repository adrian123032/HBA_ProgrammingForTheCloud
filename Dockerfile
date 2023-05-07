FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build-env
WORKDIR /app

COPY SubscriberApp/SubscriberApp.csproj SubscriberApp/
COPY Common/Common.csproj Common/
RUN dotnet restore SubscriberApp/SubscriberApp.csproj

COPY . ./
RUN dotnet publish SubscriberApp -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:3.1
WORKDIR /app
EXPOSE 80
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "SubscriberApp.dll"]