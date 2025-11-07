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
 * Icon configuration for control buttons
 * Allows customization of icon classes (e.g., Boxicons, Font Awesome, etc.)
 */
export interface IconConfig {
    fullscreen?: string;
    zoomIn?: string;
    zoomOut?: string;
    reset?: string;
    pan?: string;
    exportPng?: string;
    exportSvg?: string;
}

/**
 * Enhancement configuration options
 */
export interface EnhancementConfig {
    /**
     * Icon classes for control buttons
     * Defaults to Boxicons (bx-*)
     *
     * @example
     * // Using Font Awesome
     * icons: {
     *   fullscreen: 'fas fa-expand',
     *   zoomIn: 'fas fa-plus',
     *   // ...
     * }
     *
     * @example
     * // Using Material Icons
     * icons: {
     *   fullscreen: 'material-icons fullscreen',
     *   zoomIn: 'material-icons zoom_in',
     *   // ...
     * }
     */
    icons?: IconConfig;

    /**
     * Enable/disable specific controls
     */
    controls?: {
        fullscreen?: boolean;
        zoom?: boolean;
        pan?: boolean;
        export?: boolean;
    };
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
