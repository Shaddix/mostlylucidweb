import Sortable from 'sortablejs';
import htmx from 'htmx.org';

// Make htmx available globally
window.htmx = htmx;

// Workflow Editor
window.workflowEditor = {
  currentWorkflowId: null,
  nodes: {},
  connections: [],

  init() {
    this.initCanvas();
    this.initNodePalette();
    this.initHtmxEvents();
  },

  initCanvas() {
    const canvas = document.getElementById('workflow-canvas');
    if (!canvas) return;

    // Make nodes draggable
    const self = this;
    canvas.addEventListener('mousedown', (e) => {
      if (!e.target.classList.contains('workflow-node')) return;

      const node = e.target;
      const startX = e.clientX - node.offsetLeft;
      const startY = e.clientY - node.offsetTop;

      function moveNode(e) {
        const x = e.clientX - startX;
        const y = e.clientY - startY;
        node.style.left = x + 'px';
        node.style.top = y + 'px';

        // Save position
        self.saveNodePosition(node.dataset.nodeId, x, y);
      }

      function stopMoving() {
        document.removeEventListener('mousemove', moveNode);
        document.removeEventListener('mouseup', stopMoving);
        node.classList.remove('dragging');
      }

      node.classList.add('dragging');
      document.addEventListener('mousemove', moveNode);
      document.addEventListener('mouseup', stopMoving);
    });
  },

  initNodePalette() {
    const palette = document.getElementById('node-palette');
    if (!palette) return;

    palette.querySelectorAll('.node-palette-item').forEach(item => {
      item.addEventListener('click', (e) => {
        const nodeType = item.dataset.nodeType;
        this.addNode(nodeType);
      });
    });
  },

  initHtmxEvents() {
    // After node is added via HTMX
    document.body.addEventListener('htmx:afterSwap', (event) => {
      if (event.detail.target.id === 'workflow-canvas') {
        this.initCanvas();
      }
    });
  },

  addNode(nodeType) {
    // Get canvas center position
    const canvas = document.getElementById('workflow-canvas');
    const rect = canvas.getBoundingClientRect();
    const x = Math.random() * 400 + 100;
    const y = Math.random() * 300 + 100;

    // Use HTMX to add node
    htmx.ajax('POST', `/Workflow/AddNode`, {
      values: {
        workflowId: this.currentWorkflowId,
        nodeType: nodeType,
        x: x,
        y: y
      },
      target: '#workflow-canvas',
      swap: 'beforeend'
    });
  },

  saveNodePosition(nodeId, x, y) {
    // Debounce this call
    clearTimeout(this.savePositionTimeout);
    this.savePositionTimeout = setTimeout(() => {
      htmx.ajax('POST', `/Workflow/UpdateNodePosition`, {
        values: { nodeId, x, y }
      });
    }, 500);
  },

  deleteNode(nodeId) {
    if (confirm('Delete this node?')) {
      htmx.ajax('DELETE', `/Workflow/DeleteNode/${nodeId}`, {
        target: `#node-${nodeId}`,
        swap: 'outerHTML'
      });
    }
  },

  loadWorkflow(workflowId) {
    this.currentWorkflowId = workflowId;
    htmx.ajax('GET', `/Workflow/LoadWorkflow/${workflowId}`, {
      target: '#workflow-canvas'
    });
  },

  saveWorkflow() {
    const name = prompt('Workflow name:');
    if (!name) return;

    htmx.ajax('POST', '/Workflow/SaveWorkflow', {
      values: {
        name: name,
        workflowId: this.currentWorkflowId
      }
    }).then(() => {
      // Refresh workflow list
      htmx.ajax('GET', '/Workflow/WorkflowList', {
        target: '#workflow-list'
      });
    });
  },

  executeWorkflow() {
    if (!this.currentWorkflowId) {
      alert('No workflow loaded');
      return;
    }

    const btn = event.target;
    btn.disabled = true;
    btn.innerHTML = '<span class="loading loading-spinner"></span> Executing...';

    htmx.ajax('POST', `/Workflow/Execute/${this.currentWorkflowId}`, {
      target: '#execution-results'
    }).then(() => {
      btn.disabled = false;
      btn.innerHTML = '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14.752 11.168l-3.197-2.132A1 1 0 0010 9.87v4.263a1 1 0 001.555.832l3.197-2.132a1 1 0 000-1.664z"></path><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path></svg> Execute Workflow';
    });
  }
};

// Initialize on page load
document.addEventListener('DOMContentLoaded', () => {
  window.workflowEditor.init();
});
