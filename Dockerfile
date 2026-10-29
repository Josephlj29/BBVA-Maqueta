FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/ backend/
RUN dotnet publish backend/src/Bbva.Reconciliation.Api/Bbva.Reconciliation.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app .
COPY fixtures/ fixtures/

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080
ENV Sandbox__TestPlanPath=fixtures/sandbox-test-plan.json

EXPOSE 8080
ENTRYPOINT ["dotnet", "Bbva.Reconciliation.Api.dll"]
