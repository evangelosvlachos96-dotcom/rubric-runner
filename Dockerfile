# syntax=docker/dockerfile:1

# ---- Build stage -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached independently of source changes.
COPY global.json Directory.Build.props Directory.Packages.props CodeJudge.slnx ./
COPY src/CodeJudge.Domain/CodeJudge.Domain.csproj                 src/CodeJudge.Domain/
COPY src/CodeJudge.Application/CodeJudge.Application.csproj       src/CodeJudge.Application/
COPY src/CodeJudge.Infrastructure/CodeJudge.Infrastructure.csproj src/CodeJudge.Infrastructure/
COPY src/CodeJudge.Api/CodeJudge.Api.csproj                       src/CodeJudge.Api/
RUN dotnet restore src/CodeJudge.Api/CodeJudge.Api.csproj

COPY src/ src/
RUN dotnet publish src/CodeJudge.Api/CodeJudge.Api.csproj \
    -c Release -o /app/publish --no-restore

# ---- Runtime stage ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The Python and JavaScript evaluators shell out to these runtimes (System Design §6.5).
# `python` is what PythonEvaluator invokes on Windows; on Linux it invokes `python3`, so both
# names are made available.
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 nodejs \
    && ln -sf /usr/bin/python3 /usr/local/bin/python \
    && rm -rf /var/lib/apt/lists/*

# Untrusted code runs in-process (documented limitation); at least do not run it as root.
RUN groupadd --system codejudge && useradd --system --gid codejudge --create-home codejudge
COPY --from=build --chown=codejudge:codejudge /app/publish .
USER codejudge

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

ENTRYPOINT ["dotnet", "CodeJudge.Api.dll"]
