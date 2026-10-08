# Deploying to AWS EC2

One small EC2 instance runs the whole stack with Docker Compose. Caddy terminates HTTPS, and GitHub Actions deploys on every green build of `main`.

```
Internet ──443──▶ Caddy ──▶ web (nginx + Angular) ──/api, /hubs──▶ api (.NET) ──▶ SQL Server Express
```

## 1. Create the server (one time)

1. **Launch an instance:** Ubuntu 24.04, **t3.medium** (SQL Server needs at least 2 GB RAM; 4 GB leaves room for the API).
   It has to be x86-64, because SQL Server has no ARM image.
2. **Security group:** allow 22 (SSH, your IP only), 80 and 443.
3. **Elastic IP:** attach one, then point a DNS `A` record (e.g. `logistics.yourdomain.com`) at it.
4. **Install Docker:**
   ```bash
   curl -fsSL https://get.docker.com | sudo sh
   sudo usermod -aG docker $USER   # log out and back in
   ```
5. **Billing alarm:** add one in AWS Budgets (e.g. alert at $30/month).

## 2. Put the stack on the server

```bash
mkdir -p ~/logistics && cd ~/logistics
# copy deploy/docker-compose.prod.yml and deploy/Caddyfile here (scp or git clone)
```

Create `~/logistics/.env` with the same keys as `.env.example`, plus:

```
GHCR_OWNER=<your-github-username-lowercase>
DOMAIN=logistics.yourdomain.com
```

If the GHCR packages are private, run `docker login ghcr.io` once with a token that has `read:packages`.
You can also make the packages public in GitHub, since a portfolio demo has nothing to hide.

## 3. Connect GitHub Actions

In the repository, add these secrets under **Settings → Secrets and variables → Actions**, in a `production` environment:

| Secret | Value |
|---|---|
| `DEPLOY_HOST` | Elastic IP or DNS name |
| `DEPLOY_USER` | `ubuntu` |
| `DEPLOY_SSH_KEY` | Private key of a deploy-only key pair (its public key goes in `~/.ssh/authorized_keys`) |

From then on, every green CI run on `main` runs `deploy.yml`, which does three things:
1. builds the images;
2. pushes them to GHCR;
3. runs `docker compose pull && up -d` over SSH.

## Operations

- **Logs:** `docker compose -f docker-compose.prod.yml logs -f api`
- **Reset the demo data:** `docker compose -f docker-compose.prod.yml down -v && docker compose -f docker-compose.prod.yml up -d`.
  This drops the database volume, and the API reseeds on start.
- **Health:** `https://$DOMAIN/health`
