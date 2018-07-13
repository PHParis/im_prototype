FROM microsoft/dotnet:2.1.300-rc1-sdk
# FROM microsoft/aspnetcore-build:2.0
# WORKDIR /data
# COPY data/SPIMBENCH_small/source.ttl ./
# COPY /data/SPIMBENCH_small/target.ttl ./
# COPY /data/SPIMBENCH_small/refalign.rdf ./
WORKDIR /app/Prototype
# AS build-env

# Copy csproj and restore as distinct layers
COPY Prototype/*.csproj ./
# RUN ls -la
WORKDIR /app/PrototypeLib
COPY PrototypeLib/*.csproj ./
WORKDIR /app/Prototype
RUN dotnet restore

# Copy everything else and build
COPY Prototype/ ./
RUN ls -la
WORKDIR /app/PrototypeLib
COPY PrototypeLib/ ./
RUN ls -la
WORKDIR /app/Prototype
RUN dotnet clean
RUN dotnet build --force
RUN dotnet publish -c Release -o out


WORKDIR /app/Prototype/out

ENTRYPOINT ["dotnet", "Prototype.dll"]

# # Build runtime image
# FROM microsoft/aspnetcore:2.0
# WORKDIR /app
# COPY --from=build-env /app/Prototype/out .
# ENTRYPOINT ["dotnet", "Prototype.dll"]
# # test