import React, {useEffect, useLayoutEffect, useRef, useState} from "react";
import {useNavigate} from "react-router-dom";
import { Menu } from 'primereact/menu';
import {isUndefined} from "swr/_internal";
import { Sidebar } from 'primereact/sidebar';
import { Button } from 'primereact/button';
import {PanelMenu} from "primereact/panelmenu";
import {Toast} from "primereact/toast";
import {Dropdown} from "primereact/dropdown";
import {Avatar} from "primereact/avatar";
import {ScrollTop} from "primereact/scrolltop";
import {Divider} from "primereact/divider";
import {ConfirmDialog, confirmDialog} from 'primereact/confirmdialog';
import {useCookies} from "react-cookie";
import {Badge} from "primereact/badge";
import {isNullOrUndef} from "chart.js/helpers";


// выход из аккаунта пользователя
export function LogoutUser(removeCookie, navigate){
    fetch("http://localhost:5113/mail/logout", {
        method: "GET",
        headers: {
            "Content-Type": "application/json", // Тип содержимого
        }
    })
        .then(response => {
            // Обработка ответа от сервера
            if (!response.ok) {
                // Проверка на ошибки HTTP (4xx или 5xx)
                // Создаём ошибку, если ответ не успешный.
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            return response;
        })
        .then(data => {
            // Обработка полученных данных (data содержит JSON)
            console.log("Полученные данные:", data);
            removeCookie('currentUser');
            navigate('/login');
        })
        .catch(error => {
            // Обработка ошибок
            console.error("Ошибка:", error);
        });
}


function ShowSlideBar(){




    return(<>

    </>);
}

// собственно сборка верхней панели
export default function ShowUpperBar() {
    // используется для redirect
    const navigate = useNavigate();

    const [cookies, removeCookie] = useCookies(['currentUser']);

    const [visible, setVisible] = useState(false);

    const [accountsOption, setAccountsOption] = useState([]);

    const [curAccount, setCurAccount] = useState("");

    useEffect(() => {
        const credentialsJSON = localStorage.getItem('userCredentials');
        let data = credentialsJSON ? [JSON.parse(credentialsJSON)] : [];

        setCurAccount(data[Number(localStorage.getItem('curUser'))].email);

        let options = data.map((account) => {
            // выводим текущий аккаунт только сверху
            if (data[curAccount] === account) {
                return {
                    label: account.email,
                    command: () => {
                    }
                };
            }
            else
                return {};
        });

        options.push({ label: 'Добавить аккаунт', command: () => {} });

        setAccountsOption(options);
    }, []);

    const accept = () => LogoutUser(removeCookie, navigate);

    const reject = () => {}

    const userAcc = () => {
        if(isNullOrUndef(localStorage.getItem('curUser')))
            navigate('/login')
        else
            setVisible(true);
    };

    let items = [
        {
            label: 'Текущий аккаунт',
            items: [
                {
                    label: curAccount,
                    icon: 'pi pi-user',
                    disabled: true
                },
                {
                    label: 'Выйти',
                    icon: 'pi pi-sign-out',
                    command: () => confirmDialog({
                        message: 'Вы уже вошли в аккаунт. Хотите выйти?',
                        header: 'Подтверждение',
                        icon: 'pi pi-exclamation-triangle',
                        defaultFocus: 'accept',
                        rejectLabel: 'Нет',
                        acceptIcon: 'pi pi-sign-out',
                        acceptLabel: 'Да',
                        accept,
                        reject
                    })
                }
            ]
        },
        {
            label: 'Аккаунты',
            items: [...accountsOption]
        }
    ];

    return (
        <div className="App">
            <div style={{display: 'flex', justifyContent: 'flex-end'}}>
                <div className="profile">
                    <Avatar icon="pi pi-user"
                            size="large"
                            className="user-profile"
                            shape="circle"
                            onClick={userAcc}/>
                    <ConfirmDialog/>
                    <Sidebar visible={visible}
                             position="right"
                             onHide={() => setVisible(false)}>
                        <Menu model={items} style={{width: '100%'}}/>
                    </Sidebar>
                </div>
            </div>
            <ScrollTop/>
        </div>
    );
}
