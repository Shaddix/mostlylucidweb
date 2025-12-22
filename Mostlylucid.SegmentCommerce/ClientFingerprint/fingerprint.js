/**
 * Mostlylucid.ClientFingerprint - Zero-Cookie Session Identification
 *
 * Collects browser signals and computes a hash for session identification.
 * Only the hash is sent to the server - no raw fingerprint data is transmitted.
 *
 * Privacy:
 * - No cookies or localStorage
 * - Only a hash is sent (no raw signals)
 * - Server re-hashes with secret key for final session ID
 */
(function () {
    'use strict';

    var MLFingerprint = {
        version: '%%VERSION%%',
        endpoint: '%%ENDPOINT%%',
        config: {
            collectWebGL: %%COLLECT_WEBGL%%,
            collectCanvas: %%COLLECT_CANVAS%%,
            collectAudio: %%COLLECT_AUDIO%%,
            timeout: %%TIMEOUT%%
        },

        /**
         * Collect all fingerprint signals and return as a string
         */
        collectSignals: function () {
            var signals = [];
            var nav = navigator;
            var scr = screen;
            var win = window;

            // Basic signals
            try { signals.push(Intl.DateTimeFormat().resolvedOptions().timeZone); } catch (e) { }
            signals.push(nav.language || '');
            signals.push((nav.languages || []).join(','));
            signals.push(nav.platform || '');
            signals.push(nav.hardwareConcurrency || 0);
            signals.push(nav.deviceMemory || 0);
            signals.push(scr.width + 'x' + scr.height + 'x' + scr.colorDepth);
            signals.push(scr.availWidth + 'x' + scr.availHeight);
            signals.push(win.devicePixelRatio || 1);
            signals.push(nav.maxTouchPoints || 0);

            // WebGL
            if (this.config.collectWebGL) {
                var gl = this.getWebGL();
                signals.push(gl.vendor || '');
                signals.push(gl.renderer || '');
            }

            // Canvas
            if (this.config.collectCanvas) {
                signals.push(this.getCanvasHash());
            }

            return signals.join('|');
        },

        /**
         * Get WebGL info
         */
        getWebGL: function () {
            try {
                var canvas = document.createElement('canvas');
                var gl = canvas.getContext('webgl') || canvas.getContext('experimental-webgl');
                if (!gl) return { vendor: '', renderer: '' };
                var ext = gl.getExtension('WEBGL_debug_renderer_info');
                if (!ext) return { vendor: '', renderer: '' };
                return {
                    vendor: gl.getParameter(ext.UNMASKED_VENDOR_WEBGL) || '',
                    renderer: gl.getParameter(ext.UNMASKED_RENDERER_WEBGL) || ''
                };
            } catch (e) {
                return { vendor: '', renderer: '' };
            }
        },

        /**
         * Get canvas fingerprint hash
         */
        getCanvasHash: function () {
            try {
                var canvas = document.createElement('canvas');
                canvas.width = 200;
                canvas.height = 50;
                var ctx = canvas.getContext('2d');
                ctx.textBaseline = 'top';
                ctx.font = '14px Arial';
                ctx.fillStyle = '#f60';
                ctx.fillRect(125, 1, 62, 20);
                ctx.fillStyle = '#069';
                ctx.fillText('MLFP', 2, 15);
                ctx.fillStyle = 'rgba(102, 204, 0, 0.7)';
                ctx.fillText('MLFP', 4, 17);
                return this.hash(canvas.toDataURL());
            } catch (e) {
                return '';
            }
        },

        /**
         * Get audio fingerprint hash (async)
         */
        getAudioHash: function () {
            var self = this;
            return new Promise(function (resolve) {
                try {
                    var AudioCtx = window.OfflineAudioContext || window.webkitOfflineAudioContext;
                    if (!AudioCtx) { resolve(''); return; }

                    var ctx = new AudioCtx(1, 44100, 44100);
                    var osc = ctx.createOscillator();
                    var comp = ctx.createDynamicsCompressor();
                    osc.type = 'triangle';
                    osc.frequency.value = 1000;
                    osc.connect(comp);
                    comp.connect(ctx.destination);
                    osc.start(0);
                    ctx.startRendering();

                    ctx.oncomplete = function (e) {
                        try {
                            var buf = e.renderedBuffer.getChannelData(0);
                            var step = Math.max(1, Math.floor(buf.length / 128));
                            var str = '';
                            for (var i = 0; i < buf.length; i += step) {
                                str += String.fromCharCode(~~((buf[i] + 1) * 127));
                            }
                            resolve(self.hash(str));
                        } catch (ex) {
                            resolve('');
                        }
                    };
                } catch (e) {
                    resolve('');
                }
            });
        },

        /**
         * Simple hash function (djb2)
         */
        hash: function (str) {
            var hash = 5381;
            for (var i = 0; i < str.length; i++) {
                hash = ((hash << 5) + hash) + str.charCodeAt(i);
                hash = hash & hash; // Convert to 32-bit
            }
            return (hash >>> 0).toString(16);
        },

        /**
         * Send fingerprint hash to server
         */
        send: function (hash) {
            try {
                var payload = JSON.stringify({ h: hash, ts: Date.now() });

                if (navigator.sendBeacon) {
                    navigator.sendBeacon(this.endpoint, new Blob([payload], { type: 'application/json' }));
                } else {
                    var xhr = new XMLHttpRequest();
                    xhr.open('POST', this.endpoint, true);
                    xhr.setRequestHeader('Content-Type', 'application/json');
                    xhr.timeout = this.config.timeout;
                    xhr.send(payload);
                }
            } catch (e) { }
        },

        /**
         * Main entry point
         */
        run: function () {
            var self = this;

            setTimeout(function () {
                try {
                    var signals = self.collectSignals();

                    if (self.config.collectAudio) {
                        self.getAudioHash().then(function (audioHash) {
                            signals += '|' + audioHash;
                            self.send(self.hash(signals));
                        });
                    } else {
                        self.send(self.hash(signals));
                    }
                } catch (e) { }
            }, 50);
        }
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { MLFingerprint.run(); });
    } else {
        MLFingerprint.run();
    }
})();
