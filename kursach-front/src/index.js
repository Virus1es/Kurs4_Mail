import React from 'react';
import ReactDOM from 'react-dom/client';
import {Route, BrowserRouter, Routes} from "react-router-dom";
import './index.css';
import App from './App';
import reportWebVitals from './reportWebVitals';
import Home from "./userPages/home";
import ShowLogin from "./userPages/login";

const root = ReactDOM.createRoot(document.getElementById('root'));


root.render(
    <BrowserRouter>
          <React.StrictMode>
            <App />
          </React.StrictMode>
        <Routes>
            <Route path="/" element={<Home />} />
            <Route path="/login" element={<ShowLogin/>} />
        </Routes>
    </BrowserRouter>
);

// If you want to start measuring performance in your app, pass a function
// to log results (for example: reportWebVitals(console.log))
// or send to an analytics endpoint. Learn more: https://bit.ly/CRA-vitals
reportWebVitals();
