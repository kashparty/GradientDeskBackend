FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /src
COPY ["BackpropServer.csproj", "./"]
RUN dotnet restore "./BackpropServer.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "BackpropServer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BackpropServer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
CMD dotnet BackpropServer.dll --urls=http://+:$PORT
