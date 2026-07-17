mod commands;
mod formula;
mod models;
mod ocr;
mod processes;
mod storage;

use tauri::{
    menu::{Menu, MenuItem, PredefinedMenuItem},
    tray::{MouseButton, MouseButtonState, TrayIconBuilder, TrayIconEvent},
    Emitter, Manager, WindowEvent,
};

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .manage(processes::BoardManager::default())
        .manage(formula::FormulaManager::default())
        .manage(ocr::OcrManager::default())
        .plugin(tauri_plugin_single_instance::init(|app, _args, _cwd| {
            if let Some(window) = app.get_webview_window("main") {
                let _ = window.unminimize();
                let _ = window.show();
                let _ = window.set_focus();
            }
        }))
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_global_shortcut::Builder::new().build())
        .plugin(tauri_plugin_opener::init())
        .setup(|app| {
            let show =
                MenuItem::with_id(app, "show", "显示 Excalidraw Manager", true, None::<&str>)?;
            let formula = MenuItem::with_id(app, "formula", "打开公式编辑器", true, None::<&str>)?;
            let stop_all = MenuItem::with_id(app, "stop-all", "停止所有画板", true, None::<&str>)?;
            let separator = PredefinedMenuItem::separator(app)?;
            let quit = MenuItem::with_id(app, "quit", "退出", true, None::<&str>)?;
            let menu = Menu::with_items(app, &[&show, &formula, &stop_all, &separator, &quit])?;

            TrayIconBuilder::with_id("main")
                .icon(app.default_window_icon().expect("application icon").clone())
                .menu(&menu)
                .show_menu_on_left_click(false)
                .on_menu_event(|app, event| match event.id.as_ref() {
                    "show" => show_main_window(app),
                    "formula" => {
                        show_main_window(app);
                        let _ = app.emit("open-formula-editor", ());
                    }
                    "stop-all" => {
                        let _ = app.state::<processes::BoardManager>().stop_all();
                    }
                    "quit" => {
                        let _ = app.state::<processes::BoardManager>().stop_all();
                        let _ = app.state::<formula::FormulaManager>().stop();
                        let _ = app.state::<ocr::OcrManager>().stop();
                        app.exit(0);
                    }
                    _ => {}
                })
                .on_tray_icon_event(|tray, event| {
                    if let TrayIconEvent::Click {
                        button: MouseButton::Left,
                        button_state: MouseButtonState::Up,
                        ..
                    } = event
                    {
                        show_main_window(tray.app_handle());
                    }
                })
                .build(app)?;
            Ok(())
        })
        .on_window_event(|window, event| {
            if let WindowEvent::CloseRequested { api, .. } = event {
                api.prevent_close();
                let _ = window.hide();
            }
        })
        .invoke_handler(tauri::generate_handler![
            commands::bootstrap,
            commands::app_info,
            commands::load_settings,
            commands::save_settings,
            commands::add_workspace,
            commands::remove_workspace,
            commands::list_directory,
            commands::create_board,
            commands::create_folder,
            commands::start_board,
            commands::list_running_boards,
            commands::stop_board,
            commands::stop_all_boards,
            commands::start_formula_editor,
            commands::get_formula_editor,
            commands::stop_formula_editor,
            commands::get_formula_ocr_status,
        ])
        .run(tauri::generate_context!())
        .expect("error while running Excalidraw Manager");
}

fn show_main_window(app: &tauri::AppHandle) {
    if let Some(window) = app.get_webview_window("main") {
        let _ = window.unminimize();
        let _ = window.show();
        let _ = window.set_focus();
    }
}
