# Fcs.Donation.Worker

Worker generated with the **VSA Worker** template.

## Projects

| Project | Description |
|---|---|
| `Application` | Vertical slices, background consumers, and application services |
| `Worker` | .NET worker host, configuration, Dockerfile, and process entry point |

## Create a new project

```bash
dotnet new vsa-worker -n MyCompany.MyWorker
```

## Template options

| Flag | Default | Description |
|---|---|---|
| `--useKafka` | `true` | Adds a sample Kafka consumer with Confluent.Kafka |
| `--useSerilog` | `true` | Adds Serilog console/Seq logging |
| `--useCiCd` | `true` | Adds GitHub Actions wrappers for `fcg-pipelines` |
| `--serviceSlug` | `fcs-donation-worker` | Kebab-case name used by delivery resources |

## Running locally

```bash
dotnet restore
dotnet build
dotnet run --project src/Fcs.Donation.Worker.Worker
```

With Docker Compose:

```bash
docker compose up -d
```

## Project structure

```text
src/
  Fcs.Donation.Worker.Application/
    Features/
      SampleEvent/
  Fcs.Donation.Worker.Worker/
```
