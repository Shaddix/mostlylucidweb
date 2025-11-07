/**
 * Type definitions for @mostlylucid/mermaid-enhancements
 */

/**
 * SVG Pan Zoom instance interface
 */
export interface PanZoomInstance {
    zoom(scale: number): void;
    zoomIn(): void;
    zoomOut(): void;
    reset(): void;
    fit(): void;
    center(): void;
    resize(): void;
    destroy(): void;
    isPanEnabled(): boolean;
    enablePan(enabled: boolean): void;
}

/**
 * Export format type
 */
export type ExportFormat = 'png' | 'svg';

/**
 * Theme type
 */
export type Theme = 'dark' | 'default';

/**
 * Control button action types
 */
export type ControlAction = 'fullscreen' | 'zoomIn' | 'zoomOut' | 'reset' | 'pan' | 'exportPng' | 'exportSvg';

/**
 * Control button configuration
 */
export interface ControlButton {
    icon: string;
    title: string;
    action: ControlAction;
}

/**
 * Mermaid initialization options
 */
export interface MermaidOptions {
    startOnLoad: boolean;
    theme: Theme;
    themeVariables?: {
        background?: string;
        [key: string]: any;
    };
    [key: string]: any;
}

/**
 * Global window interface extensions
 */
declare global {
    interface Window {
        mermaid?: {
            initialize(options: MermaidOptions): void;
            run(options?: { querySelector?: string }): Promise<void>;
        };
        __themeState?: 'dark' | 'light';
        enhanceMermaidDiagrams?: () => void;
        cleanupMermaidEnhancements?: () => void;
        initMermaid?: () => Promise<void>;
    }
}

export {};
