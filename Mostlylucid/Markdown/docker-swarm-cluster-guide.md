# From Docker Compose to Docker Swarm: Running a Production Cluster on 8 Crappy Mini PCs

<datetime class="hidden">2025-11-11T14:00</datetime>
<!--category-- Docker, DevOps, Containers, Docker Swarm, High Availability -->

## Introduction

So you've been running your blog on a single server (perhaps a Hetzner auction box) with Docker Compose, and now you've got 8 mini PCs and SBCs gathering dust (rescued from eBay for under £100 each, all destined for e-waste). What do you do? Build a proper Docker Swarm cluster, of course!

This article details how to convert the [Docker Compose setup](/blog/dockercompose) I've been running into a highly available Docker Swarm cluster. We'll cover everything from initial cluster setup to running PostgreSQL with replication, setting up monitoring with Prometheus and Grafana across multiple nodes, and managing the whole thing without losing your sanity.

**What we're building:**
- A Docker Swarm cluster across 8 Ubuntu Server nodes (rescued eBay hardware + ARM SBCs)
- High availability for all services
- PostgreSQL with replication and failover
- Distributed monitoring with Prometheus and Grafana
- Load-balanced web services
- Persistent storage that survives node failures
- Automated health checks and rolling updates

**What you'll need:**
- 8 machines running Ubuntu Server (in my case, eBay rescue mini PCs plus ARM SBCs)
- Basic networking knowledge
- The existing Docker Compose setup from previous articles
- Patience (Swarm has quirks)
- A willingness to troubleshoot networking issues at 2am
- All my hardware has SSDs and cost under £100 each from eBay (x86) or retail (ARM SBCs)

[TOC]

## Docker Swarm Fundamentals: What You're Getting Into

### What is Docker Swarm?

Docker Swarm is Docker's native clustering and orchestration solution. It turns a pool of Docker hosts into a single, virtual Docker host. Think of it as Docker Compose's older, more serious sibling who went to business school.

**Key concepts:**

```bash
# The mental model
Docker Compose: Single machine, multiple containers
Docker Swarm:   Multiple machines, multiple containers, one logical unit

# Swarm components
Manager Nodes: Control plane - schedule tasks, maintain cluster state
Worker Nodes:  Run your containers (called "tasks" in Swarm)
Services:      Containers that run across the cluster
Stacks:        Groups of services (like docker-compose.yml)
```

### Swarm vs Compose: What's Different?

| Feature | Docker Compose | Docker Swarm |
|---------|---------------|--------------|
| **Deployment Target** | Single host | Multi-host cluster |
| **High Availability** | No | Yes |
| **Load Balancing** | Manual | Built-in |
| **Scaling** | Manual (`docker-compose scale`) | Declarative (`replicas: 3`) |
| **Rolling Updates** | No | Yes |
| **Health Checks** | Basic | Advanced with auto-restart |
| **Secrets Management** | `.env` files | Docker Secrets |
| **Volume Driver** | Local only | Distributed options |
| **Configuration** | `docker-compose.yml` | `docker-stack.yml` (similar) |

### The Three-Way Decision: Compose vs Swarm vs Kubernetes

Choosing the right orchestration solution depends on your scale, complexity, and team capabilities. Here's how they compare:

#### Docker Compose: The Simple Single-Node Solution

**Best for:**
- Development environments
- Small applications (<5 services)
- Single-server deployments
- When you can tolerate downtime for updates
- Learning Docker fundamentals

**Pros:**
- Dead simple to understand and use
- YAML is straightforward
- Fast iteration (edit file, `docker-compose up`)
- Minimal resource overhead
- Perfect for a $40/month Hetzner auction box
- Great debugging experience

**Cons:**
- No high availability
- No built-in load balancing
- Manual scaling
- Updates require downtime
- Single point of failure

**Example use cases:**
- Personal blog on one server (what I was running before)
- Development environment
- Small business app with low traffic
- Proof of concept

**Cost:** $40-80/month (single Hetzner auction box - great value bare metal servers - or similar VPS)

#### Docker Swarm: The Sweet Spot for Small Clusters

**Best for:**
- 3-50 node clusters
- Teams comfortable with Docker Compose
- When you need HA without K8s complexity
- Self-hosted infrastructure
- Budget-conscious setups using recycled hardware

**Pros:**
- Built into Docker (zero additional installation)
- If you know Compose, you mostly know Swarm
- High availability out of the box
- Rolling updates with zero downtime
- Built-in load balancing and service discovery
- Secrets management
- Simple to troubleshoot
- Low resource overhead (runs fine on old mini PCs)

**Cons:**
- Limited ecosystem compared to K8s
- No advanced scheduling (node affinity is basic)
- Storage orchestration is manual (NFS, etc.)
- Smaller community than Kubernetes
- Docker, Inc. has de-emphasised it (though still supported)
- No Helm-equivalent package manager
- Advanced networking requires workarounds

**Example use cases:**
- Multi-node blog with HA (what I'm running now)
- Small SaaS with <100k users
- Internal company tools
- Homelab projects
- Learning orchestration without K8s complexity

**Cost:** $0 if using old hardware, or $120-200/month for 3-5 cloud VMs

#### Kubernetes: The Enterprise-Grade Orchestrator

**Best for:**
- Large-scale deployments (50+ nodes)
- Complex microservices architectures
- When you need the ecosystem (Istio, Helm, Operators)
- Teams with dedicated DevOps engineers
- When you need advanced scheduling and autoscaling
- Multi-cloud or hybrid cloud deployments

**Pros:**
- Industry standard (huge community, tons of tools)
- Extremely powerful and flexible
- Advanced scheduling and autoscaling
- Rich ecosystem (Helm, Operators, Istio, Knative, etc.)
- Declarative configuration with powerful primitives
- StatefulSets for databases
- Multiple storage backends (Rook, Longhorn, etc.)
- RBAC and security policies
- Federation across clusters

**Cons:**
- Steep learning curve (3-6 months to proficiency)
- Complex to set up and maintain
- Requires significant resources (minimum 3 control plane nodes)
- Overkill for simple applications
- YAML hell (manifests are verbose)
- Breaking changes between versions
- Need dedicated staff or managed service ($$$$)

**Example use cases:**
- Netflix-scale microservices
- Multi-tenant SaaS platforms
- Enterprise applications
- When you need auto-scaling based on custom metrics
- When you have a team of DevOps engineers

**Cost:** $300-500/month minimum (managed K8s like GKE, EKS, AKS), or significant DevOps salary if self-managed

#### Direct Comparison Table

| Feature | Docker Compose | Docker Swarm | Kubernetes |
|---------|---------------|--------------|------------|
| **Setup Time** | 10 minutes | 1-2 hours | 1-2 days (or weeks to master) |
| **Learning Curve** | Easy | Medium | Steep |
| **Nodes Supported** | 1 | 1-50 | 1-5000+ |
| **High Availability** | ❌ No | ✅ Yes | ✅ Yes |
| **Auto-Scaling** | ❌ No | ⚠️ Manual | ✅ Yes (HPA, VPA, Cluster Autoscaler) |
| **Rolling Updates** | ❌ No | ✅ Yes | ✅ Yes |
| **Rollback** | ❌ Manual | ✅ One command | ✅ One command |
| **Load Balancing** | ❌ Manual | ✅ Built-in | ✅ Built-in |
| **Service Discovery** | ❌ Manual links | ✅ Built-in DNS | ✅ Built-in DNS + Service Mesh options |
| **Secrets Management** | `.env` files | Docker Secrets | Kubernetes Secrets + external (Vault) |
| **Config Management** | `.env` files | Docker Configs | ConfigMaps |
| **Storage Orchestration** | Local volumes | Limited (NFS, plugins) | Advanced (CSI, StatefulSets) |
| **Monitoring** | DIY | DIY | Rich ecosystem (Prometheus Operator) |
| **Package Manager** | ❌ No | ❌ No | ✅ Helm |
| **Operators/Extensions** | ❌ No | ❌ No | ✅ Yes (CRDs, Operators) |
| **Resource Requirements** | ~500MB RAM | ~1GB RAM | ~4GB RAM (control plane) |
| **Community Size** | Large | Medium | Massive |
| **Job Stability** | Good for devs | Niche | Hot job market skill |
| **Complexity** | ⭐ Simple | ⭐⭐ Moderate | ⭐⭐⭐⭐⭐ Complex |

#### When to Choose Each

**Choose Docker Compose when:**
- You're running on a single Hetzner auction box ($40/month)
- Your app is a monolith or <5 microservices
- You can afford occasional downtime for updates
- You value simplicity over all else
- You're a solo developer or small team
- You're learning Docker

**Choose Docker Swarm when:**
- You have 3-8 mini PCs or 3-5 VMs you want to utilise
- You need HA but don't want K8s complexity
- You know Docker Compose and want to scale up
- You're self-hosting on budget hardware
- Your team is 1-5 people without dedicated DevOps
- You want zero-downtime deployments without the learning curve
- You value operational simplicity

**Choose Kubernetes when:**
- You're running 10+ microservices
- You have dedicated DevOps team or budget for managed K8s
- You need advanced features (autoscaling, StatefulSets, Operators)
- You're deploying across multiple cloud providers
- You need the ecosystem (Istio, Knative, etc.)
- You have >50 nodes or plan to scale there
- You need sophisticated scheduling (GPU workloads, etc.)
- "Resume-driven development" (K8s looks great on CVs)

#### My Decision: Swarm for 8 Machines

For my setup (6 eBay rescue x86 mini PCs plus 2 ARM SBCs running the mostlylucid blog), Swarm was the obvious choice:

**Swarm advantages for me:**
- Learned it in one weekend vs months for K8s
- Runs brilliantly on my mix of x86 and ARM64 machines (2-core to 8-core)
- Zero-downtime deployments for free
- Built into Docker (no separate installation)
- Can troubleshoot without Googling cryptic K8s errors
- Feels like Compose but with superpowers

**Why not K8s (though I could build it):**
- I've used Kubernetes and Portainer before, but wanted simpler
- Overkill for a blog (even with ML translation service)
- Control plane alone would consume 2-3 mini PCs
- Would spend more time managing K8s than writing content
- Don't need Helm, Operators, or service meshes
- Wanted orchestration that doesn't require constant care

**Why not stay on Compose:**
- Wanted zero-downtime updates
- Wanted to utilise all 8 machines (rescued from e-waste and ARM SBCs)
- Wanted high availability (node failures shouldn't take site down)
- Wanted to learn clustering without K8s commitment

**The result:** Production-grade HA blog cluster, manageable by one person, running on a mix of rescued eBay x86 hardware and ARM single-board computers for under £800 total. Perfect.

**Note on Portainer:** Tools like [Portainer](https://www.portainer.io/) provide excellent web UIs for managing both Docker Swarm and Kubernetes clusters. Portainer can manage either orchestrator and makes many tasks easier through its dashboard. However, setting up and configuring Portainer is beyond the scope of this article - we'll focus on native Docker Swarm commands you can use from any manager node.

## Planning Your Cluster Architecture

### Node Configuration

**My 8-node cluster (eBay rescues + ARM SBCs, all with SSDs, under £100 each):**

| Node | Role | Hardware | Arch | Specs | Services |
|------|------|----------|------|-------|----------|
| `swarm-manager-1` | Manager + Worker | Dell 8-core | x86_64 | 8GB RAM, 8 cores, SSD | Etcd, Monitoring, Web |
| `swarm-manager-2` | Manager + Worker | Orange Pi 5 | ARM64 | 6GB RAM, 8 cores, SSD | Etcd, Monitoring, Web |
| `swarm-manager-3` | Manager + Worker | Dell 2-core | x86_64 | 4GB RAM, 2 cores, SSD | Etcd, Monitoring |
| `swarm-worker-1` | Worker | Lenovo 8-core | x86_64 | 8GB RAM, 8 cores, SSD | PostgreSQL Primary, heavy workloads |
| `swarm-worker-2` | Worker | Raspberry Pi 5 | ARM64 | 8GB RAM, 4 cores, SSD | PostgreSQL Replica, ARM workloads |
| `swarm-worker-3` | Worker | Dell 2-core | x86_64 | 4GB RAM, 2 cores, SSD | General workloads |
| `swarm-worker-4` | Worker | Dell 2-core | x86_64 | 4GB RAM, 2 cores, SSD | Translation service |
| `swarm-worker-5` | Worker | Dell 2-core | x86_64 | 4GB RAM, 2 cores, SSD | Background jobs, cache |

**Why 3 managers?**
- Swarm uses Raft consensus (needs odd number: 1, 3, 5, or 7)
- 3 managers can tolerate 1 failure
- 5 managers can tolerate 2 failures (overkill for us)
- Never run even numbers of managers

### Network Architecture

```mermaid
graph TB
    Internet[Internet]

    subgraph "External Load Balancer"
        LB[Caddy / Nginx]
    end

    subgraph "Swarm Managers"
        M1[Manager 1<br/>10.0.1.10]
        M2[Manager 2<br/>10.0.1.11]
        M3[Manager 3<br/>10.0.1.12]
    end

    subgraph "Swarm Workers"
        W1[Worker 1<br/>10.0.1.20<br/>PostgreSQL Primary]
        W2[Worker 2<br/>10.0.1.21<br/>PostgreSQL Replica]
        W3[Worker 3<br/>10.0.1.22]
        W4[Worker 4<br/>10.0.1.23]
        W5[Worker 5<br/>10.0.1.24]
    end

    subgraph "Overlay Networks"
        FrontendNet[frontend-network<br/>Web services]
        BackendNet[backend-network<br/>Database, internal]
        MonitorNet[monitoring-network<br/>Prometheus, Grafana]
    end

    Internet --> LB
    LB --> M1
    LB --> M2
    LB --> M3

    M1 --> FrontendNet
    M2 --> FrontendNet
    M3 --> FrontendNet

    W1 --> BackendNet
    W2 --> BackendNet

    FrontendNet --> BackendNet
    MonitorNet --> FrontendNet
    MonitorNet --> BackendNet
```

**Key networking decisions:**
- Use static IPs for all nodes (10.0.1.10-24 in my setup)
- Create overlay networks for service isolation
- Use ingress network for external access
- Keep PostgreSQL on backend network only

## Setting Up the Swarm Cluster

### Prerequisites on Each Node

```bash
# On all 8 nodes, run these commands

# Update system
sudo apt update && sudo apt upgrade -y

# Install Docker (if not already installed)
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh

# Add your user to docker group (avoid sudo)
sudo usermod -aG docker $USER

# Enable Docker to start on boot
sudo systemctl enable docker
sudo systemctl start docker

# Verify Docker installation
docker --version
```

### Initialise the Swarm

**On your first manager node (swarm-manager-1):**

```bash
# Initialise Swarm with explicit advertise address
docker swarm init --advertise-addr 10.0.1.10

# Output will look like:
# Swarm initialized: current node (abc123) is now a manager.
#
# To add a worker to this swarm, run the following command:
#     docker swarm join --token SWMTKN-1-xxxxx 10.0.1.10:2377
#
# To add a manager to this swarm, run:
#     docker swarm join-token manager

# Save these tokens! You'll need them.
```

**Important ports to open in firewall:**

```bash
# On all nodes, ensure these ports are accessible:

# Cluster management
sudo ufw allow 2377/tcp    # Swarm cluster management
sudo ufw allow 7946/tcp    # Container network discovery
sudo ufw allow 7946/udp
sudo ufw allow 4789/udp    # Overlay network traffic

# Optional: SSH
sudo ufw allow 22/tcp

# Enable firewall
sudo ufw enable
```

### Join Manager Nodes

**On swarm-manager-2 and swarm-manager-3:**

```bash
# Get the manager join token from manager-1
# (Run this on manager-1)
docker swarm join-token manager

# Copy the output command and run on manager-2 and manager-3
docker swarm join --token SWMTKN-1-xxxxx-manager-token 10.0.1.10:2377
```

**Verify managers:**

```bash
# On any manager node
docker node ls

# Output should show 3 managers:
# ID               HOSTNAME            STATUS    AVAILABILITY   MANAGER STATUS
# abc123 *         swarm-manager-1     Ready     Active         Leader
# def456           swarm-manager-2     Ready     Active         Reachable
# ghi789           swarm-manager-3     Ready     Active         Reachable
```

### Join Worker Nodes

**On each worker node (worker-1 through worker-5):**

```bash
# Get the worker join token from a manager
# (Run this on any manager)
docker swarm join-token worker

# Copy the output command and run on each worker
docker swarm join --token SWMTKN-1-xxxxx-worker-token 10.0.1.10:2377
```

**Verify cluster:**

```bash
# On any manager node
docker node ls

# You should see all 8 nodes:
# ID               HOSTNAME            STATUS    AVAILABILITY   MANAGER STATUS
# abc123 *         swarm-manager-1     Ready     Active         Leader
# def456           swarm-manager-2     Ready     Active         Reachable
# ghi789           swarm-manager-3     Ready     Active         Reachable
# jkl012           swarm-worker-1      Ready     Active
# mno345           swarm-worker-2      Ready     Active
# pqr678           swarm-worker-3      Ready     Active
# stu901           swarm-worker-4      Ready     Active
# vwx234           swarm-worker-5      Ready     Active
```

### Label Nodes for Placement

Labels help you control where services run:

```bash
# Label database nodes (workers with more RAM)
docker node update --label-add type=database swarm-worker-1
docker node update --label-add type=database swarm-worker-2

# Label the primary PostgreSQL node
docker node update --label-add postgres.primary=true swarm-worker-1

# Label monitoring nodes (managers)
docker node update --label-add type=monitoring swarm-manager-1
docker node update --label-add type=monitoring swarm-manager-2
docker node update --label-add type=monitoring swarm-manager-3

# Label background job node
docker node update --label-add type=worker swarm-worker-5

# Verify labels
docker node inspect swarm-worker-1 --format '{{.Spec.Labels}}'
```

## Creating Overlay Networks

Overlay networks allow containers on different hosts to communicate:

```bash
# Create frontend network (web services)
docker network create \
  --driver overlay \
  --attachable \
  frontend-network

# Create backend network (database, internal services)
docker network create \
  --driver overlay \
  --attachable \
  backend-network

# Create monitoring network
docker network create \
  --driver overlay \
  --attachable \
  monitoring-network

# List networks
docker network ls | grep overlay
```

**Why `--attachable`?**
- Allows standalone containers (not just services) to attach
- Useful for debugging and one-off tasks
- Enables `docker run --network frontend-network`

## Converting Docker Compose to Stack Files

Swarm uses a slightly different format than Compose. Here's how to convert.

### Key Differences

```yaml
# Docker Compose
version: '3.8'
services:
  web:
    build: .                    # ❌ Not supported in Swarm
    image: myapp:latest         # ✅ Use pre-built images
    container_name: web         # ❌ Swarm manages names
    ports:
      - "8080:8080"             # ✅ Works, but...
    deploy:                     # ✅ Swarm-specific section
      replicas: 3               # Number of instances
      placement:
        constraints:
          - node.role==worker
      update_config:
        parallelism: 1
        delay: 10s
      restart_policy:
        condition: on-failure
```

**What Swarm doesn't support:**
- `build:` - Pre-build images and push to registry
- `container_name:` - Swarm generates names
- `depends_on:` - Use healthchecks instead
- `links:` - Use overlay networks
- `restart: always` - Use `deploy.restart_policy`

### Basic Stack File Structure

**docker-stack.yml:**

```yaml
version: '3.8'

services:
  # Service definitions

networks:
  # Network definitions

volumes:
  # Volume definitions (limited in Swarm)

secrets:
  # Docker secrets (better than .env files)

configs:
  # Docker configs (non-sensitive configuration files)
```

## Setting Up Persistent Storage for Swarm

This is where Swarm gets tricky. Unlike Compose, volumes don't automatically replicate across nodes.

### Storage Options

**Option 1: Local volumes with node constraints** (simplest, what I use)
**Option 2: NFS shared storage** (better for HA)
**Option 3: REX-Ray volume plugin** (best for production)
**Option 4: GlusterFS or Ceph** (overkill for 8 nodes)

### Option 1: Local Volumes with Constraints

Pin services to specific nodes with their data:

```yaml
services:
  postgres-primary:
    image: postgres:16-alpine
    volumes:
      - postgres-data:/var/lib/postgresql/data
    deploy:
      placement:
        constraints:
          - node.labels.postgres.primary==true  # Always runs on worker-1

volumes:
  postgres-data:
    driver: local  # Data stays on worker-1
```

**Pros:**
- Simple to set up
- Fast (local disk)
- Works with existing hardware

**Cons:**
- Service tied to specific node
- Node failure = service down until manual intervention
- No automatic failover for stateful services

### Option 2: NFS Shared Storage (Recommended)

**Set up NFS server (on manager-1):**

```bash
# On manager-1 (10.0.1.10)
sudo apt install nfs-kernel-server -y

# Create shared directories
sudo mkdir -p /mnt/swarm-storage/postgres
sudo mkdir -p /mnt/swarm-storage/grafana
sudo mkdir -p /mnt/swarm-storage/prometheus
sudo mkdir -p /mnt/swarm-storage/seq

# Set permissions
sudo chown -R nobody:nogroup /mnt/swarm-storage
sudo chmod -R 777 /mnt/swarm-storage

# Configure NFS exports
sudo nano /etc/exports

# Add these lines:
/mnt/swarm-storage/postgres    10.0.1.0/24(rw,sync,no_subtree_check,no_root_squash)
/mnt/swarm-storage/grafana     10.0.1.0/24(rw,sync,no_subtree_check,no_root_squash)
/mnt/swarm-storage/prometheus  10.0.1.0/24(rw,sync,no_subtree_check,no_root_squash)
/mnt/swarm-storage/seq         10.0.1.0/24(rw,sync,no_subtree_check,no_root_squash)

# Apply exports
sudo exportfs -a
sudo systemctl restart nfs-kernel-server
```

**Set up NFS clients (on all other nodes):**

```bash
# On all nodes except manager-1
sudo apt install nfs-common -y

# Test mount (replace with your NFS server IP)
sudo mount -t nfs 10.0.1.10:/mnt/swarm-storage/postgres /mnt/test

# If successful, unmount
sudo umount /mnt/test
```

**Use NFS volumes in stack:**

```yaml
volumes:
  postgres-data:
    driver: local
    driver_opts:
      type: nfs
      o: addr=10.0.1.10,rw,sync
      device: ":/mnt/swarm-storage/postgres"

  grafana-data:
    driver: local
    driver_opts:
      type: nfs
      o: addr=10.0.1.10,rw,sync
      device: ":/mnt/swarm-storage/grafana"
```

**Pros:**
- Services can run on any node
- Survives node failures
- Standard Linux tech

**Cons:**
- Single point of failure (NFS server)
- Network overhead
- NFS can be finnicky

## PostgreSQL with Replication in Swarm

Running PostgreSQL in Swarm with high availability requires careful planning.

### Strategy: Primary-Replica with Patroni

**Why Patroni?**
- Automatic failover
- Health checking
- Leader election using Etcd
- Works great in Swarm

**Architecture:**

```
Etcd Cluster (3 nodes, consensus)
    ↓
Patroni (manages PostgreSQL)
    ↓
PostgreSQL Primary (writes)
    ↓ (replication)
PostgreSQL Replica(s) (reads)
```

### Etcd Cluster for Patroni

**etcd-stack.yml:**

```yaml
version: '3.8'

services:
  etcd-1:
    image: quay.io/coreos/etcd:v3.5.11
    command:
      - /usr/local/bin/etcd
      - --name=etcd-1
      - --initial-advertise-peer-urls=http://etcd-1:2380
      - --listen-peer-urls=http://0.0.0.0:2380
      - --listen-client-urls=http://0.0.0.0:2379
      - --advertise-client-urls=http://etcd-1:2379
      - --initial-cluster=etcd-1=http://etcd-1:2380,etcd-2=http://etcd-2:2380,etcd-3=http://etcd-3:2380
      - --initial-cluster-state=new
      - --initial-cluster-token=etcd-cluster
    networks:
      - backend-network
    deploy:
      placement:
        constraints:
          - node.hostname==swarm-manager-1
      restart_policy:
        condition: on-failure

  etcd-2:
    image: quay.io/coreos/etcd:v3.5.11
    command:
      - /usr/local/bin/etcd
      - --name=etcd-2
      - --initial-advertise-peer-urls=http://etcd-2:2380
      - --listen-peer-urls=http://0.0.0.0:2380
      - --listen-client-urls=http://0.0.0.0:2379
      - --advertise-client-urls=http://etcd-2:2379
      - --initial-cluster=etcd-1=http://etcd-1:2380,etcd-2=http://etcd-2:2380,etcd-3=http://etcd-3:2380
      - --initial-cluster-state=new
      - --initial-cluster-token=etcd-cluster
    networks:
      - backend-network
    deploy:
      placement:
        constraints:
          - node.hostname==swarm-manager-2
      restart_policy:
        condition: on-failure

  etcd-3:
    image: quay.io/coreos/etcd:v3.5.11
    command:
      - /usr/local/bin/etcd
      - --name=etcd-3
      - --initial-advertise-peer-urls=http://etcd-3:2380
      - --listen-peer-urls=http://0.0.0.0:2380
      - --listen-client-urls=http://0.0.0.0:2379
      - --advertise-client-urls=http://etcd-3:2379
      - --initial-cluster=etcd-1=http://etcd-1:2380,etcd-2=http://etcd-2:2380,etcd-3=http://etcd-3:2380
      - --initial-cluster-state=new
      - --initial-cluster-token=etcd-cluster
    networks:
      - backend-network
    deploy:
      placement:
        constraints:
          - node.hostname==swarm-manager-3
      restart_policy:
        condition: on-failure

networks:
  backend-network:
    external: true
```

**Deploy Etcd:**

```bash
docker stack deploy -c etcd-stack.yml etcd

# Verify etcd cluster
docker exec $(docker ps -q -f name=etcd-1) etcdctl member list
```

### Patroni + PostgreSQL Stack

**postgres-stack.yml:**

```yaml
version: '3.8'

services:
  postgres-primary:
    image: patroni/patroni:latest
    hostname: postgres-primary
    environment:
      PATRONI_SCOPE: postgres-cluster
      PATRONI_NAME: postgres-primary
      PATRONI_ETCD3_HOSTS: etcd-1:2379,etcd-2:2379,etcd-3:2379
      PATRONI_RESTAPI_LISTEN: 0.0.0.0:8008
      PATRONI_RESTAPI_CONNECT_ADDRESS: postgres-primary:8008
      PATRONI_POSTGRESQL_LISTEN: 0.0.0.0:5432
      PATRONI_POSTGRESQL_CONNECT_ADDRESS: postgres-primary:5432
      PATRONI_POSTGRESQL_DATA_DIR: /var/lib/postgresql/data
      PATRONI_REPLICATION_USERNAME: replicator
      PATRONI_REPLICATION_PASSWORD_FILE: /run/secrets/postgres_replication_password
      PATRONI_SUPERUSER_USERNAME: postgres
      PATRONI_SUPERUSER_PASSWORD_FILE: /run/secrets/postgres_password
    secrets:
      - postgres_password
      - postgres_replication_password
    volumes:
      - postgres-primary-data:/var/lib/postgresql/data
    networks:
      - backend-network
    deploy:
      placement:
        constraints:
          - node.labels.type==database
          - node.labels.postgres.primary==true
      restart_policy:
        condition: on-failure
        delay: 5s
        max_attempts: 3

  postgres-replica:
    image: patroni/patroni:latest
    environment:
      PATRONI_SCOPE: postgres-cluster
      PATRONI_NAME: postgres-replica-{{.Task.Slot}}  # Unique per replica
      PATRONI_ETCD3_HOSTS: etcd-1:2379,etcd-2:2379,etcd-3:2379
      PATRONI_RESTAPI_LISTEN: 0.0.0.0:8008
      PATRONI_RESTAPI_CONNECT_ADDRESS: postgres-replica-{{.Task.Slot}}:8008
      PATRONI_POSTGRESQL_LISTEN: 0.0.0.0:5432
      PATRONI_POSTGRESQL_CONNECT_ADDRESS: postgres-replica-{{.Task.Slot}}:5432
      PATRONI_POSTGRESQL_DATA_DIR: /var/lib/postgresql/data
      PATRONI_REPLICATION_USERNAME: replicator
      PATRONI_REPLICATION_PASSWORD_FILE: /run/secrets/postgres_replication_password
      PATRONI_SUPERUSER_USERNAME: postgres
      PATRONI_SUPERUSER_PASSWORD_FILE: /run/secrets/postgres_password
    secrets:
      - postgres_password
      - postgres_replication_password
    networks:
      - backend-network
    deploy:
      replicas: 2  # Two replicas
      placement:
        constraints:
          - node.labels.type==database
      restart_policy:
        condition: on-failure
        delay: 5s
        max_attempts: 3

  # HAProxy for PostgreSQL load balancing
  postgres-lb:
    image: haproxy:2.9-alpine
    ports:
      - "5432:5432"  # External PostgreSQL port
    configs:
      - source: haproxy_config
        target: /usr/local/etc/haproxy/haproxy.cfg
    networks:
      - backend-network
      - frontend-network
    deploy:
      replicas: 1
      placement:
        constraints:
          - node.role==manager

networks:
  backend-network:
    external: true
  frontend-network:
    external: true

volumes:
  postgres-primary-data:
    driver: local
    driver_opts:
      type: nfs
      o: addr=10.0.1.10,rw,sync
      device: ":/mnt/swarm-storage/postgres"

secrets:
  postgres_password:
    external: true
  postgres_replication_password:
    external: true

configs:
  haproxy_config:
    file: ./haproxy.cfg
```

**haproxy.cfg (for PostgreSQL load balancing):**

```haproxy
global
    maxconn 100

defaults
    log global
    mode tcp
    retries 2
    timeout client 30m
    timeout connect 4s
    timeout server 30m
    timeout check 5s

listen postgres
    bind *:5432
    option httpchk
    http-check expect status 200
    default-server inter 3s fall 3 rise 2 on-marked-down shutdown-sessions
    server postgres-primary postgres-primary:5432 maxconn 100 check port 8008
    server postgres-replica-1 postgres-replica-1:5432 maxconn 100 check port 8008
    server postgres-replica-2 postgres-replica-2:5432 maxconn 100 check port 8008
```

**Create secrets:**

```bash
# Create PostgreSQL passwords as Docker secrets
echo "your-super-secret-password" | docker secret create postgres_password -
echo "your-replication-password" | docker secret create postgres_replication_password -

# List secrets
docker secret ls
```

**Deploy PostgreSQL stack:**

```bash
docker stack deploy -c postgres-stack.yml postgres

# Watch services start
docker service ls | grep postgres

# Check Patroni cluster status
docker exec $(docker ps -q -f name=postgres-primary) patronictl -c /etc/patroni.yml list
```

**Simpler Alternative: Single PostgreSQL with Backups**

If Patroni is too complex:

```yaml
services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_PASSWORD_FILE: /run/secrets/postgres_password
    secrets:
      - postgres_password
    volumes:
      - postgres-data:/var/lib/postgresql/data
    networks:
      - backend-network
    deploy:
      placement:
        constraints:
          - node.labels.postgres.primary==true
      restart_policy:
        condition: on-failure
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  postgres-data:
    driver: local
    driver_opts:
      type: nfs
      o: addr=10.0.1.10,rw,sync
      device: ":/mnt/swarm-storage/postgres"
```

**Trade-off:** Simpler setup, but no automatic failover.

## Converting the Full Mostlylucid Stack

Now let's convert the complete [Docker Compose setup](/blog/docker-development-deep-dive) to a Swarm stack.

**mostlylucid-stack.yml:**

```yaml
version: '3.8'

services:
  # Main ASP.NET Core application
  mostlylucid:
    image: scottgal/mostlylucid:latest
    environment:
      - Auth__GoogleClientId=${AUTH_GOOGLECLIENTID}
      - Auth__GoogleClientSecret=${AUTH_GOOGLECLIENTSECRET}
      - Auth__AdminUserGoogleId=${AUTH_ADMINUSERGOOGLEID}
      - SmtpSettings__UserName=${SMTPSETTINGS_USERNAME}
      - SmtpSettings__Password=${SMTPSETTINGS_PASSWORD}
      - Analytics__UmamiPath=${ANALYTICS_UMAMIPATH}
      - Analytics__WebsiteId=${ANALYTICS_WEBSITEID}
      - ConnectionStrings__DefaultConnection=Host=postgres-lb;Database=mostlylucid;Username=postgres;Password=${POSTGRES_PASSWORD}
      - TranslateService__ServiceIPs=easynmt:8888
      - Serilog__WriteTo__0__Args__apiKey=${SEQ_API_KEY}
    secrets:
      - postgres_password
    networks:
      - frontend-network
      - backend-network
    volumes:
      - mostlylucid-markdown:/app/markdown
      - mostlylucid-logs:/app/logs
      - mostlylucid-cache:/app/wwwroot/cache
    deploy:
      replicas: 3  # Run on 3 nodes for HA
      update_config:
        parallelism: 1  # Update one at a time
        delay: 10s
        failure_action: rollback
      rollback_config:
        parallelism: 1
        delay: 5s
      restart_policy:
        condition: on-failure
        delay: 5s
        max_attempts: 3
      labels:
        - "traefik.enable=true"
        - "traefik.http.routers.mostlylucid.rule=Host(`mostlylucid.net`)"
        - "traefik.http.services.mostlylucid.loadbalancer.server.port=8080"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/healthz"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s

  # Umami analytics
  umami:
    image: ghcr.io/umami-software/umami:postgresql-latest
    environment:
      DATABASE_URL: postgresql://postgres:${POSTGRES_PASSWORD}@postgres-lb:5432/umami
      DATABASE_TYPE: postgresql
      HASH_SALT: ${HASH_SALT}
      APP_SECRET: ${APP_SECRET}
      TRACKER_SCRIPT_NAME: getinfo
      API_COLLECT_ENDPOINT: all
    secrets:
      - postgres_password
    networks:
      - frontend-network
      - backend-network
    deploy:
      replicas: 2
      restart_policy:
        condition: on-failure

  # Translation service (CPU-limited)
  easynmt:
    image: easynmt/api:2.0.2-cpu
    networks:
      - backend-network
    volumes:
      - easynmt-cache:/cache
    deploy:
      replicas: 1
      placement:
        constraints:
          - node.labels.type==worker
      resources:
        limits:
          cpus: '4.0'
          memory: 4G
        reservations:
          cpus: '2.0'
          memory: 2G
      restart_policy:
        condition: on-failure

  # Traefik reverse proxy (replaces Caddy)
  traefik:
    image: traefik:v2.11
    command:
      - "--api.dashboard=true"
      - "--providers.docker=true"
      - "--providers.docker.swarmMode=true"
      - "--providers.docker.exposedbydefault=false"
      - "--entrypoints.web.address=:80"
      - "--entrypoints.websecure.address=:443"
      - "--certificatesresolvers.letsencrypt.acme.httpchallenge=true"
      - "--certificatesresolvers.letsencrypt.acme.httpchallenge.entrypoint=web"
      - "--certificatesresolvers.letsencrypt.acme.email=your@email.com"
      - "--certificatesresolvers.letsencrypt.acme.storage=/letsencrypt/acme.json"
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
      - traefik-certs:/letsencrypt
    networks:
      - frontend-network
    deploy:
      placement:
        constraints:
          - node.role==manager
      restart_policy:
        condition: on-failure

  # Seq centralized logging
  seq:
    image: datalust/seq:latest
    environment:
      ACCEPT_EULA: "Y"
      SEQ_FIRSTRUN_ADMINPASSWORDHASH: ${SEQ_DEFAULT_HASH}
    volumes:
      - seq-data:/data
    networks:
      - monitoring-network
      - frontend-network
    deploy:
      replicas: 1
      placement:
        constraints:
          - node.role==manager
      restart_policy:
        condition: on-failure

  # Prometheus metrics collection
  prometheus:
    image: prom/prometheus:latest
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
      - '--storage.tsdb.retention.time=15d'
    configs:
      - source: prometheus_config
        target: /etc/prometheus/prometheus.yml
    volumes:
      - prometheus-data:/prometheus
    networks:
      - monitoring-network
      - frontend-network
      - backend-network
    deploy:
      replicas: 1
      placement:
        constraints:
          - node.labels.type==monitoring
      restart_policy:
        condition: on-failure

  # Grafana visualization
  grafana:
    image: grafana/grafana:latest
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=${GRAFANA_PASSWORD}
      - GF_INSTALL_PLUGINS=grafana-piechart-panel
    volumes:
      - grafana-data:/var/lib/grafana
    networks:
      - monitoring-network
      - frontend-network
    deploy:
      replicas: 1
      placement:
        constraints:
          - node.labels.type==monitoring
      restart_policy:
        condition: on-failure

  # Node exporter on every node
  node-exporter:
    image: quay.io/prometheus/node-exporter:latest
    command:
      - '--path.rootfs=/host'
    volumes:
      - '/:/host:ro,rslave'
    networks:
      - monitoring-network
    deploy:
      mode: global  # Run on every node
      restart_policy:
        condition: on-failure

  # cAdvisor for container metrics
  cadvisor:
    image: gcr.io/cadvisor/cadvisor:latest
    volumes:
      - /:/rootfs:ro
      - /var/run:/var/run:ro
      - /sys:/sys:ro
      - /var/lib/docker/:/var/lib/docker:ro
      - /dev/disk/:/dev/disk:ro
    networks:
      - monitoring-network
    deploy:
      mode: global  # Run on every node
      restart_policy:
        condition: on-failure

networks:
  frontend-network:
    external: true
  backend-network:
    external: true
  monitoring-network:
    external: true

volumes:
  mostlylucid-markdown:
    driver: local
    driver_opts:
      type: nfs
      o: addr=10.0.1.10,rw,sync
      device: ":/mnt/swarm-storage/markdown"

  mostlylucid-logs:
    driver: local
    driver_opts:
      type: nfs
      o: addr=10.0.1.10,rw,sync
      device: ":/mnt/swarm-storage/logs"

  mostlylucid-cache:
    driver: local
    driver_opts:
      type: nfs
      o: addr=10.0.1.10,rw,sync
      device: ":/mnt/swarm-storage/cache"

  easynmt-cache:
    driver: local
    driver_opts:
      type: nfs
      o: addr=10.0.1.10,rw,sync
      device: ":/mnt/swarm-storage/easynmt"

  seq-data:
    driver: local
    driver_opts:
      type: nfs
      o: addr=10.0.1.10,rw,sync
      device: ":/mnt/swarm-storage/seq"

  prometheus-data:
    driver: local
    driver_opts:
      type: nfs
      o: addr=10.0.1.10,rw,sync
      device: ":/mnt/swarm-storage/prometheus"

  grafana-data:
    driver: local
    driver_opts:
      type: nfs
      o: addr=10.0.1.10,rw,sync
      device: ":/mnt/swarm-storage/grafana"

  traefik-certs:
    driver: local
    driver_opts:
      type: nfs
      o: addr=10.0.1.10,rw,sync
      device: ":/mnt/swarm-storage/traefik"

secrets:
  postgres_password:
    external: true

configs:
  prometheus_config:
    file: ./prometheus.yml
```

**prometheus.yml (for Swarm service discovery):**

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  # Swarm tasks discovery
  - job_name: 'docker-swarm'
    dockerswarm_sd_configs:
      - host: unix:///var/run/docker.sock
        role: tasks
    relabel_configs:
      - source_labels: [__meta_dockerswarm_service_name]
        target_label: service

  # Node exporters
  - job_name: 'node-exporter'
    dns_sd_configs:
      - names:
          - 'tasks.node-exporter'
        type: 'A'
        port: 9100

  # cAdvisor
  - job_name: 'cadvisor'
    dns_sd_configs:
      - names:
          - 'tasks.cadvisor'
        type: 'A'
        port: 8080

  # Static PostgreSQL
  - job_name: 'postgres'
    static_configs:
      - targets: ['postgres-lb:9187']
```

## Deploying the Stack

```bash
# Ensure networks exist
docker network create --driver overlay --attachable frontend-network
docker network create --driver overlay --attachable backend-network
docker network create --driver overlay --attachable monitoring-network

# Create secrets (if not already created)
echo "your-postgres-password" | docker secret create postgres_password -

# Deploy the stack
docker stack deploy -c mostlylucid-stack.yml mostlylucid

# Watch services come up
watch docker service ls

# Check logs of a specific service
docker service logs -f mostlylucid_mostlylucid

# Check which node each task is running on
docker service ps mostlylucid_mostlylucid
```

## Scaling Services

```bash
# Scale the web application to 5 replicas
docker service scale mostlylucid_mostlylucid=5

# Scale down to 2
docker service scale mostlylucid_mostlylucid=2

# Scale multiple services at once
docker service scale \
  mostlylucid_mostlylucid=3 \
  mostlylucid_umami=2 \
  mostlylucid_easynmt=2
```

## Rolling Updates

```bash
# Update to a new image version
docker service update \
  --image scottgal/mostlylucid:v2.0.0 \
  mostlylucid_mostlylucid

# Swarm will:
# 1. Pull new image on all nodes
# 2. Stop one task
# 3. Start new task with new image
# 4. Wait for healthcheck to pass
# 5. Repeat for next task

# Force update (redeployment)
docker service update --force mostlylucid_mostlylucid

# Rollback to previous version
docker service rollback mostlylucid_mostlylucid
```

## Monitoring the Swarm

### Visualizer - See Where Containers Run

```yaml
# Add to your stack or run separately
visualizer:
  image: dockersamples/visualizer:latest
  ports:
    - "8080:8080"
  volumes:
    - /var/run/docker.sock:/var/run/docker.sock:ro
  deploy:
    placement:
      constraints:
        - node.role==manager
  networks:
    - frontend-network
```

Access at http://manager-ip:8080 to see a live cluster map.

### Portainer - Full Swarm UI

```bash
# Deploy Portainer as a service
docker service create \
  --name portainer \
  --publish 9000:9000 \
  --publish 8000:8000 \
  --constraint 'node.role==manager' \
  --mount type=bind,src=/var/run/docker.sock,dst=/var/run/docker.sock \
  --mount type=volume,src=portainer-data,dst=/data \
  portainer/portainer-ce:latest
```

Access Portainer at http://manager-ip:9000 for a full web UI.

### Command-Line Monitoring

```bash
# View all services
docker service ls

# View all nodes
docker node ls

# View all tasks (containers) across cluster
docker service ps $(docker service ls -q)

# View stack services
docker stack services mostlylucid

# View resource usage per node
docker node ps swarm-worker-1
docker node ps swarm-worker-2

# Filter tasks by state
docker service ps --filter "desired-state=running" mostlylucid_mostlylucid

# Inspect service configuration
docker service inspect --pretty mostlylucid_mostlylucid
```

## Handling Node Failures

### Simulate Node Failure

```bash
# Drain a node (move all services off it)
docker node update --availability drain swarm-worker-3

# Watch services migrate
watch docker service ps mostlylucid_mostlylucid

# Bring node back
docker node update --availability active swarm-worker-3
```

### What Happens When a Node Dies?

1. **Worker node dies:**
   - Swarm detects failure within ~30 seconds
   - Tasks are rescheduled on healthy nodes
   - Services remain available (if you have replicas)

2. **Manager node dies:**
   - Remaining managers maintain quorum (if you have 3+)
   - No impact on services
   - Promote a worker to manager if needed:
     ```bash
     docker node promote swarm-worker-1
     ```

3. **NFS server dies:**
   - Services with NFS volumes fail
   - This is why we constrain critical services to specific nodes
   - Or use more robust storage (GlusterFS, Ceph)

## Backups in Swarm

### Backup Swarm State

```bash
# On a manager node
sudo systemctl stop docker
sudo tar -czvf swarm-backup.tar.gz /var/lib/docker/swarm
sudo systemctl start docker

# Copy backup off-site
scp swarm-backup.tar.gz backup-server:/backups/
```

### Backup PostgreSQL

```bash
# Dump from primary
docker exec $(docker ps -q -f name=postgres-primary) \
  pg_dumpall -U postgres > backup.sql

# Or automated with a service
services:
  pg-backup:
    image: prodrigestivill/postgres-backup-local:latest
    environment:
      POSTGRES_HOST: postgres-lb
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD_FILE: /run/secrets/postgres_password
      SCHEDULE: "0 2 * * *"  # 2am daily
    secrets:
      - postgres_password
    volumes:
      - backups:/backups
    deploy:
      placement:
        constraints:
          - node.role==manager
```

## Common Swarm Gotchas

### 1. Volumes Don't Replicate

**Problem:** Local volumes don't move with services.

**Solution:** Use NFS or other distributed storage, or constrain services to nodes.

### 2. Port Conflicts

**Problem:** Published ports are cluster-wide.

```yaml
# This fails if two services want port 80
services:
  web1:
    ports:
      - "80:8080"  # ❌
  web2:
    ports:
      - "80:8080"  # ❌ Conflict!
```

**Solution:** Use Traefik/HAProxy for routing, publish on different ports, or use ingress.

### 3. Service Discovery Delays

**Problem:** DNS takes ~5-10 seconds to update after deployment.

**Solution:** Use healthchecks, add retry logic in your app.

### 4. Secrets Can't Be Updated

**Problem:** Docker secrets are immutable.

**Solution:** Create new secret with different name, update service to use it.

```bash
# Create new version of secret
echo "new-password" | docker secret create postgres_password_v2 -

# Update service to use new secret
docker service update \
  --secret-rm postgres_password \
  --secret-add postgres_password_v2 \
  mostlylucid_postgres

# Remove old secret
docker secret rm postgres_password
```

### 5. Build Not Supported

**Problem:** `docker stack deploy` doesn't support `build:`.

**Solution:** Build images separately, push to registry, reference in stack file.

```bash
# Build and push before deploying
docker build -t registry.example.com/myapp:latest .
docker push registry.example.com/myapp:latest

# Then deploy
docker stack deploy -c stack.yml myapp
```

## Comparison: Swarm vs Compose for This Setup

| Aspect | Docker Compose | Docker Swarm |
|--------|---------------|--------------|
| **High Availability** | No | Yes (3 replicas) |
| **Node Failure** | App dies | Auto-reschedule |
| **Load Balancing** | Manual (Caddy) | Built-in ingress |
| **Rolling Updates** | Manual downtime | Zero-downtime |
| **Resource Usage** | 1 node, ~2GB RAM | 8 nodes, ~46GB RAM total |
| **Complexity** | Low | Medium |
| **Setup Time** | 30 minutes | 4 hours |
| **Troubleshooting** | Easy (`docker logs`) | Harder (distributed) |
| **Cost** | $40/month (Hetzner auction box) | $0 (using old hardware) or power costs |

## When Swarm Isn't Worth It

**Stick with Docker Compose if:**
- You have <3 nodes
- Your app is simple (blog, small site)
- You can tolerate downtime for updates
- You're on a single server (Hetzner auction box, VPS, etc.)
- You value simplicity over HA
- You don't have spare hardware lying about

**Use Swarm if:**
- You have 3+ nodes
- You need zero-downtime deployments
- You need to scale horizontally
- You have spare hardware to use
- You want to learn orchestration without K8s complexity

## Conclusion

Migrating from Docker Compose to Docker Swarm transforms your single-node setup into a resilient, highly available cluster. You get:

**Benefits:**
- **High Availability:** Services survive node failures
- **Load Balancing:** Built-in request distribution
- **Zero-Downtime Updates:** Rolling deployments
- **Horizontal Scaling:** Add replicas with one command
- **Resource Utilisation:** Use all 8 machines efficiently (x86 and ARM)

But you also inherit:

**Challenges:**
- **Complexity:** More moving parts to understand
- **Storage Challenges:** Volumes don't replicate automatically
- **Networking Quirks:** Overlay networks can be finicky
- **Debugging Difficulty:** Logs spread across nodes

**My experience running this setup:**

After 6 months running mostlylucid on an 8-node Swarm cluster built from eBay rescue x86 mini PCs and ARM SBCs:

**Pros:**
- Rock-solid uptime (99.9%+)
- Can update without anyone noticing
- Survived multiple node failures with no downtime
- Feels professional (it's basically mini production)
- Great learning experience

**Cons:**
- Took a weekend to set up properly
- NFS hiccups caused headaches twice
- Power consumption is higher than a single Hetzner box (8 machines drawing ~75W total vs ~15W for one server)
- More hardware to maintain physically
- ARM SBCs require multi-arch Docker images
- Overkill for a personal blog

**Would I do it again?**
Absolutely, but for learning and fun, not because a blog *needs* this much infrastructure. If you're running on a single Hetzner auction box ($40/month for great value bare metal servers, like this very site runs on) with Docker Compose and it works fine, there's no compelling reason to migrate unless you want to learn clustering or need true HA. For production work at scale? Swarm hits the sweet spot between Compose simplicity and Kubernetes power.

**Next Steps:**

- Monitor your cluster with Prometheus + Grafana
- Set up automated backups for PostgreSQL
- Experiment with scaling during traffic spikes
- Consider adding CI/CD with automatic deployments
- Maybe try Kubernetes next?

Remember: the best infrastructure is infrastructure that works reliably without needing constant attention. Swarm delivers that once you've climbed the initial learning curve.

## Further Reading

- [Docker Swarm Official Docs](https://docs.docker.com/engine/swarm/)
- [Swarm Visualiser](https://github.com/dockersamples/docker-swarm-visualizer)
- [Patroni Documentation](https://patroni.readthedocs.io/)
- [Traefik Swarm Configuration](https://doc.traefik.io/traefik/providers/docker/)
- [My previous Docker articles](/blog/dockercompose)
