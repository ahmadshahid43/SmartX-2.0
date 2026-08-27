# Oracle Cloud Always Free VM

This is the best **zero monthly cost** path for the current SmartX stack when you do **not** want a
paid PaaS like Render.

## Why this path

- one small Linux VM can run the SmartX API **and** web app together
- the app keeps using `LocalJson`, so no SQL Server is needed
- Docker volume storage on the VM keeps your runtime data and login keys
- later, the same deployment can switch to `Supabase/Postgres`

## What this path is good for

- demos
- pilot clients
- low-traffic live use
- cost-sensitive launch

## Before you start

You need:

1. An Oracle Cloud account with an **Always Free** compute VM.
2. Ubuntu on the VM.
3. SSH access to the VM.
4. This repository already pushed to GitHub.

## Files already prepared in this repo

- `Dockerfile`
- `docker-compose.oracle-free.yml`
- `.env.oracle.example`

## Fast deployment steps

On the Oracle VM:

1. Install Git and Docker.
2. Clone the repo.
3. Copy `.env.oracle.example` to `.env`.
4. Change the owner password and owner email in `.env`.
5. Start the app with Docker Compose.

## Suggested commands on the VM

```bash
sudo apt update
sudo apt install -y git ca-certificates curl
```

Install Docker using the official Docker repository instructions for Ubuntu, then:

```bash
git clone https://github.com/ahmadshahid43/SmartX-2.0.git
cd SmartX-2.0
cp .env.oracle.example .env
nano .env
sudo docker compose -f docker-compose.oracle-free.yml up -d --build
```

## What to put in `.env`

At minimum:

```dotenv
SMARTX_OWNER_PASSWORD=YourStrongOwnerPassword
SMARTX_OWNER_EMAIL=owner@yourcompany.com
SMARTX_OWNER_NAME=SmartX Owner
PERSISTENCE_PROVIDER=LocalJson
PERSISTENCE_CONNECTION_STRING=
```

## First login behavior

On first production boot:

- SmartX creates runtime data in the Docker volume
- the seeded owner password is replaced by your custom owner password
- non-owner demo users are locked automatically

That means the old demo credentials are **not** valid on public hosting.

## Open the app

After the container starts:

- `http://<your-vm-public-ip>/` opens SmartX
- `http://<your-vm-public-ip>/swagger` opens the API explorer
- `http://<your-vm-public-ip>/health` checks app health

## Later switch to Supabase

If you want cloud database later, only change:

```dotenv
PERSISTENCE_PROVIDER=Supabase
PERSISTENCE_CONNECTION_STRING=Host=<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=smartx_app;Password=<secret>;SSL Mode=Require;Pooling=true;Maximum Pool Size=10
```

Then restart:

```bash
sudo docker compose -f docker-compose.oracle-free.yml up -d --build
```

## Reality check

This is the best free fit for the current stack, but it is still a self-managed VM:

- you handle Oracle account setup
- you handle VM firewall/security list
- you handle SSH access

In return, monthly hosting cost can stay at **zero** if you remain inside Oracle's Always Free
limits.
