FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
# Restore and publish in one step: splitting them across separate RUN layers
# (restore, then a later --no-restore publish) produced a project.assets.json
# missing the linux-x64 target under Vercel's buildah-based builder, even
# though the same split worked under local Docker/BuildKit. ReadyToRun
# pre-JITs the app at build time so a cold start doesn't also pay JIT cost.
RUN dotnet publish -c Release -o /app/publish -r linux-x64 --self-contained false \
    -p:PublishReadyToRun=true

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ShepherdMeet.dll"]
