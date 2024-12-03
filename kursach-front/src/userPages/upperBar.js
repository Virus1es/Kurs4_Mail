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
export function LogoutUser(navigate){
    const credentialsJSON = localStorage.getItem('userCredentials');
    const data = credentialsJSON ? JSON.parse(credentialsJSON) : [];
    const index = parseInt(localStorage.getItem('curUser'));

    // удаляем аккаунт из списка
    data.splice(index, 1);

    if(data.length === 0) {
        localStorage.removeItem('userCredentials');
        localStorage.removeItem('curUser');
    }
    else {
        localStorage.setItem('userCredentials', JSON.stringify(data));
        localStorage.setItem('curUser', '0');
    }

    window.location.reload();
}

// собственно сборка верхней панели
export default function ShowUpperBar() {
    // используется для redirect
    const navigate = useNavigate();

    const [visible, setVisible] = useState(false);

    const [accountsOption, setAccountsOption] = useState([]);

    const [curAccount, setCurAccount] = useState(null);

    const accept = () => LogoutUser(navigate);

    const reject = () => {}

    const userAcc = () => {
        if(isNullOrUndef(localStorage.getItem('curUser')))
            navigate('/login')
        else
            setVisible(true);
    };

    useEffect(() => {
        if(!isNullOrUndef(localStorage.getItem('userCredentials'))){
            const credentialsJSON = localStorage.getItem('userCredentials');

            let data = credentialsJSON ? JSON.parse(credentialsJSON) : [];

            const index = parseInt(localStorage.getItem('curUser'));

            setCurAccount(data[index]?.email);
        }
    }, []);


    const setNewUser = (newEmail) => {
        const credentialsJSON = localStorage.getItem('userCredentials');

        let data = credentialsJSON ? JSON.parse(credentialsJSON) : [];

        let index = data.findIndex(account => account.email === newEmail);

        localStorage.setItem('curUser', `${index}`);

        window.location.reload();
    }

    useEffect(() => {
        if(!isNullOrUndef(localStorage.getItem('userCredentials')) && curAccount !== null) {
            const credentialsJSON = localStorage.getItem('userCredentials');
            let data = credentialsJSON ? JSON.parse(credentialsJSON) : [];

            // не выводим текущий аккаунт ещё и снизу
            let options = data.filter(obj => obj.email !== curAccount)
                .map((account) => {
                    return {
                        label: account.email,
                        command: () => {
                            setNewUser(account.email);
                        }
                    };
                });

            options.push({
                label: 'Добавить аккаунт',
                icon: 'pi pi-plus-circle',
                command: () => {
                    setVisible(false);
                    navigate('/login');
                }
            });

            setAccountsOption(options);
        }
    }, [curAccount]);


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
